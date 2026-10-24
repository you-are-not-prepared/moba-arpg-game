---
name: unity-playtest
description: Drive the open Unity Editor from the shell to test moba-test in play mode — run C# in the Editor, wait for compiles, enter/leave play mode, press keys, click UI Toolkit elements, take Game view screenshots, and measure things frame by frame with probes. Use whenever a change to moba-test should be checked in the running game (offline PvE, menus, HUD, abilities), and always before saying a gameplay or UI change works. Includes the rules that keep the user's own login and Editor state untouched.
---

# Unity playtest (moba-test)

Test changes in the real game by driving the Unity Editor the user has open, through the `unity` CLI (`unity command eval`, `editor_play`, ...). Helpers live in `scripts/unity.sh`.

## Setup

```bash
source .claude/skills/unity-playtest/scripts/unity.sh   # from the moba-test folder; it cd's there
```

| Helper | Does |
|---|---|
| `ev '<C#>'` / `evv` | Runs C# in the Editor; prints the `return` value (`evv`: bare value) |
| `recompile [expr]` | Refresh + compile, waits; `expr` = C# bool that's true once the new code is in |
| `compiled [expr]`, `errors`, `clearconsole` | Wait for a compile; console warnings/errors counted |
| `play [Scene]` / `stop` | Play from a scene (default Frontend) / stop and reopen Frontend |
| `token_aside` / `token_use T` / `token_restore` | Keep the user's login out of tests (see Rules) |
| `key Q`, `uiclick GO "$(byname GO name)"`, `byclass GO class i` | Key press; click a UI Toolkit element |
| `shot name` / `cleanshots` | Game view → `Assets/Temp/name.png` (read it) / delete them |
| `probe_wait secs` | Waits for a probe (see below) and prints its result |
| `revert_noise` | Reverts Unity's own settings flips before committing |

## Workflow

1. **Compile first:** `recompile 'System.Type.GetType("Moba.X, Assembly-CSharp") != null'` for a new type; then `errors` must be empty (apart from known noise like "No RuntimePipelineConfig asset found").
2. **Set up a match without clicking through menus** when the menus aren't what's being tested:
   ```bash
   play
   ev 'Moba.MatchSettings.Mode = Moba.MatchMode.PvE; Moba.MatchSettings.Lineup.Clear(); void Add(Moba.Team t, string id, bool bot, bool local) => Moba.MatchSettings.Lineup.Add(new Moba.LobbySlot { team = t, heroId = id, isBot = bot, isLocal = local }); Add(Moba.Team.Blue, "kael", false, true); Add(Moba.Team.Red, "vesh", true, false); UnityEngine.SceneManagement.SceneManager.LoadScene("Map_Zones"); return "ok";'
   ```
   Hero ids: kael, vesh, brann, torvald, nyx. Maps: Map_Classic, Map_Zones, Map_Delve. Speed up with `ev 'Moba.GameManager.Instance.SetGameSpeed(4f); return "ok";'`.
3. **Act and check** through the game's own code paths. Private HUD/controller methods can be reached with reflection, e.g. `typeof(Moba.UI.Hud).GetMethod("TryBuy", BindingFlags.NonPublic | BindingFlags.Instance)`. That tests what a click would do, so prefer it over poking state directly.
4. **Look** when it's visual: `shot name`, then read `Assets/Temp/name.png`.
5. **Finish:** `stop`, `token_restore` (if used), `cleanshots`, `revert_noise`.

## Probes: anything faster than a second

Each CLI call takes about a second, so quick things (a projectile's flight, a cooldown ticking, a turn) are missed between calls. Copy `scripts/probe-template.cs`. It hooks `EditorApplication.update`, records every frame (or only changes), and leaves the result in `SessionState "probe"`. Then:

```bash
ev "$(cat /tmp/my-probe.cs)"; probe_wait 30
```

Writing to a log only when a value changes (with a timestamp) answers "did it snap or turn gradually?" questions well.

## Rules (the user asked for these)

- **The user's account is `mobatest`; tests must never touch it.** Before any play-mode test that might reach the backend (login, PvP, match history), run `token_aside`. To test logged in, register a throwaway `tester_*` account (`curl -X POST localhost:8080/auth/register ...`), then `token_use "$TOKEN"` *before* `play`. Afterwards run `token_restore` and delete the test account (see the pvp-e2e-test skill for the SQL). Never print the user's token, and never log out through the UI or `/auth/logout`: that ends their real session.
- **Leave the Editor as the user expects it:** play mode stopped, `Assets/_Project/Scenes/Frontend.unity` open (Play starts whatever scene is open), input settings restored (`stop` does all three).
- **Don't commit test leftovers:** `Assets/Temp`, and `ProjectSettings/UnityConnectSettings.asset` when Unity flipped `m_Enabled` by itself.

## Pitfalls

- **5-second limit:** an `ev` that runs longer fails with "Main thread operation timed out after 5000ms", but the work usually goes on. For long jobs (builds), start them and poll for the result instead of waiting on the call.
- **Queued work may never run:** `EditorApplication.delayCall` only runs when the Editor ticks. When the Editor sits idle in the background it can stall indefinitely. If a queued job didn't run, call the work directly in `ev`, and clear leftovers with `ev 'UnityEditor.EditorApplication.delayCall = null; return "cleared";'`.
- **Eval snippets** are statement lists: fully qualified names (`UnityEngine.`, `Moba.`), local functions are fine, classes aren't, and a `return` is required. Avoid `$"..."` with nested quotes inside bash double quotes; single-quote the whole snippet.
- **`Application.runInBackground`** must be on (`testinput`) or the game freezes when the Editor loses focus.
- **The game's offline/online switch** is `Moba.Net.NetSession`: `IsRemoteClient` (connected to a game server) and `HasAuthority` (offline or server). Offline tests run with `HasAuthority == true`.
