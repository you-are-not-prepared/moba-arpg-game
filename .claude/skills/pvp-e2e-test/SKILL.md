---
name: pvp-e2e-test
description: End-to-end test of PvP in moba-test: the Unity Editor joins the local Docker game server as a player, optionally logged in as a throwaway tester_* backend account, then checks orders, spells, shop, sync, leaving/rejoining and match recording, and cleans up. Use to verify anything that runs through the game server (Scripts/Net, NetworkHero/NetworkUnit/NetworkMatch, GameServer, GameClient, server-side recording) or when the user reports a PvP bug.
---

# PvP end-to-end test (moba-test + game server + moba-backend)

The Editor plays the client, Docker runs the server, and the backend records matches. Uses the unity-playtest and game-server-deploy helpers.

```bash
source .claude/skills/pvp-e2e-test/scripts/pvp.sh   # loads all three helper sets
```

| Helper | Does |
|---|---|
| `tester_create tester_x` → token | Throwaway backend account (names must start with `tester_`) |
| `tester_delete tester_x`, `tester_list` | Remove it with its matches, sessions and profile; list leftovers |
| `pvp_join hero name`, `pvp_leave` | Find a match from the Frontend: through the queue, lock in the hero, Start now, wait for the match; leave |
| `pvp_cheat Level 5` | Server cheats: Level, Gold, NoManaCost, NoCooldowns (needs `MOBA_CHEATS: "on"`) |
| `wait_recorded <iso time>` | Wait for the server's `[Matches]` log line after that time |
| `last_match <token>` | The account's newest recorded match, per player |

## Before

1. **The server runs the current code:** `build_server` and `deploy` if game code or scenes changed since the last build (see game-server-deploy).
2. **The backend is up** if recording or login is involved: `curl -s localhost:8080/health` → `{"status":"ok"...}`, otherwise `docker compose up -d` in `../moba-backend`.
3. **Keep the user's login out:** `token_aside`. For a logged-in test: `T=$(tester_create tester_<something>)`, then `token_use "$T"`, both **before** `play`.

## A test run

```bash
token_aside; T=$(tester_create tester_pvp1); token_use "$T"
play                                 # Frontend
ev 'return Moba.Online.AccountService.Profile.username;'    # must be tester_pvp1
START=$(date -u +%Y-%m-%dT%H:%M:%SZ)
pvp_join kael tester_pvp1
server_log 4                         # "Client 1 (tester_pvp1, logged in) joined as kael."
# ... exercise the change: orders through the hero's controller (h.OrderMove, h.BeginCast, h.LevelUp),
#     the HUD through reflection (TryBuy, PerformLoadout, TrySellSlot), probes for fast things
wait_recorded "$START"; last_match "$T"
```

Ways to look at the client state:
- **Your hero:** `Moba.GameManager.Instance.hero`. Its `Remote` is the NetworkHero sending orders.
- **Every hero:** `GameManager.Instance.heroes`.
- **Units:** `Moba.Unit.All`.
- **Synced state:** health, mana, cooldowns and the kit on the client come from the server, so reading them checks the sync.

Orders from the client go through `HeroController` (`OrderMove`, `IssueAttackMove`, `BeginCast`, `LevelUp`). In PvP these send to the server, the same path a real click takes.

## Tips

- **Long tests:** bots only have a Fireball for now, so matches last a while. For tests without bots, use `deploy "$(nobots_override)"`, and plain `deploy` afterwards.
- **Fast effects** (projectiles, cooldowns, turning, pop-ups): count or time them with a probe (unity-playtest), not with separate `ev` calls.
- **Two players:** build the Windows client from the Editor with `ev 'Moba.EditorTools.GameBuilder.BuildWindowsClient(out string m); return m;'`, then poll `Builds/WindowsClient/MobaTest_Data/Managed/Assembly-CSharp.dll`. Start it first, so it takes blue:
  ```bash
  powershell -NoProfile -Command "Start-Process -FilePath '$(cygpath -w "$PWD/Builds/WindowsClient/MobaTest.exe")' -ArgumentList '-screen-fullscreen','0','-screen-width','1280','-screen-height','720','-join','kael','-name','P1'"
  ```
  With `-join` it searches at once and locks in that hero. The Editor then searches within 15 s, lands in the same champion select on red, and its view (shop, fog, HUD sides) can be inspected. The built game logs to `%USERPROFILE%\AppData\LocalLow\DefaultCompany\moba-test\Player.log`. Close it afterwards with `taskkill //IM MobaTest.exe //F`.
- **Queue and champion select** happen in the Frontend scene: `Moba.Net.GameClient.Lobby` is the server's state (phase, map, seats, time left), and the menu's PvP screen shows it. To test the lobby UI itself, call `StartPvP` on the FrontendController (reflection) and click `hero-tile`, `lockButton` and `startNowButton` with `uiclick Frontend ...`.
- **A running match** only takes back a logged-in player who left it. Anyone else gets "A match is already running…": restart the server (`docker compose restart game-server`) for a fresh match.
- **Leaving and rejoining:** after `pvp_leave`, a logged-in account that joins again gets its old hero back ("rejoined and took back ..."). An anonymous player gets a new hero.

## After (always)

```bash
stop                      # play mode off, Frontend open, input restored
token_restore             # the user's login back
tester_delete tester_pvp1; tester_list   # no tester_* left behind
cleanshots; revert_noise
deploy                    # if the no-bots override was used
```

Never touch the user's account `mobatest`. Its matches must stay as they were. If something did land on it, find exactly those rows, delete only them, and tell the user.
