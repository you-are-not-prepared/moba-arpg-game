---
name: game-server-deploy
description: Build the headless Linux game server from the open Unity Editor and (re)start it in Docker (docker-compose.yml in moba-arpg-game). Use after any change to game code the server runs (Scripts/, especially Scripts/Net/), after rebuilding scenes or prefabs (MOBA > Rebuild All Scenes), when changing server settings (map, bots, cheats, backend), or whenever the user says the game server should be rebuilt, restarted or checked.
---

# Game server: build and deploy (moba-arpg-game)

The game server is this same Unity project, built as a headless Linux server (`Builds/LinuxServer`, ignored by git), running in Docker on UDP `127.0.0.1:7777`. Clients only work against a server built from the same code and scenes.

```bash
source .claude/skills/game-server-deploy/scripts/server.sh   # also loads the unity-playtest helpers
build_server      # build from the open Editor, wait for new output, check the Editor's target, revert settings noise
deploy            # docker compose up -d --build, wait for "Waiting for players", show the server log
server_log        # [GameServer]/[Matches] lines and real errors only
```

## When a rebuild is needed

- **Any code change the server runs.** That means almost everything under `Assets/_Project/Scripts`, and always `Scripts/Net`, `Scripts/Online/MatchReporter.cs` and the shared gameplay code.
- **After `MOBA > Rebuild All Scenes`, or any change to scenes or prefabs.** In-scene NetworkObjects (towers, cores, the match) are matched between server and client by IDs saved in the scenes, and networked prefabs by hash. A stale server means "NetworkPrefab hash was not found" or soft-sync errors on clients.
- **Not needed** for client-only UI changes (`UI/*`), backend changes, or settings changes in `docker-compose.yml` (`deploy` alone applies those).

## Steps

1. Make sure the Editor has compiled the change (`recompile` from unity-playtest) and isn't in play mode.
2. `build_server`. The first `ev` may report "Network error" or a timeout while the Editor is busy building; that's fine, the script waits for the new build output. It prints `Server built to Builds/LinuxServer (88 MB) in 20s.` and `editor target: StandaloneWindows64/Player, http: DevelopmentOnly`.
   - If the target is still `StandaloneLinux64/Server`, switch it back (`EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64)` and `standaloneBuildSubtarget = Player`). Otherwise the Editor itself compiles with `UNITY_SERVER` and starts as a server in play mode.
3. `deploy`. The log should show:
   ```
   [GameServer] Listening on UDP 7777. Map: Zones, up to 3 bots per team, cheats on, recording matches at http://host.docker.internal:8080.
   [GameServer] Waiting for players.
   ```
4. Check `git status`. Only intended files should be changed (`ProjectSettings/ProjectSettings.asset` must not be: the build restores `insecureHttpOption` and saves).

## Server settings (`docker-compose.yml`, environment)

| Setting | Values |
|---|---|
| `MOBA_MAP` | `classic`, `zones`, `delve`, `random` (default) |
| `MOBA_BOTS` | bots per team for the seats players don't fill in champion select (0 = players only) |
| `MOBA_CHEATS` | `on` lets players use the cheat menu; **must be `off` before anyone else plays** |
| `MOBA_BACKEND_URL` / `MOBA_BACKEND_KEY` | where finished matches are recorded; the key must equal moba-arpg-backend's `GAME_SERVER_KEY` |

For a test without bots, don't edit the file: `deploy "$(nobots_override)"`, and plain `deploy` afterwards to put the normal server back.

## Behaviour worth knowing

- One match per run: queue (15 s after the first player) → champion select (60 s, or 5 s after everyone locked in, or Start now) → the map loads. When a core falls, the server quits after 15 s and Docker restarts it for the next match.
- Bots currently buy only a Fireball for Q (`BotBrain.fullShopping` off). With full shopping and `TestPrices` (1-gold items), they'd buy everything at once and end matches in about 1–3 minutes.
- Unity's headless noise in the log is harmless: "Built-in Resource Error: dereference potentially...", and "Trying to access a shader but no shaders were included".
- The server reaches the backend on the host via `host.docker.internal` (see `extra_hosts`). The backend must be running (`docker compose up -d` in moba-arpg-backend) for matches to be recorded.

## Pitfalls

- **Build output unchanged** means the build didn't run or failed. Read `errors`. A missing *Linux Dedicated Server Build Support* module gives "Build target 'StandaloneLinux64' not supported" (install it through Unity Hub, then restart the Editor).
- **Command-line build with the Editor closed:** `unity build --target StandaloneLinux64 --execute-method Moba.EditorTools.GameBuilder.BuildLinuxServerBatch .`. It switches the target back to Windows when done.
- **`ProjectSettings/UnityConnectSettings.asset`** gets `m_Enabled` flipped by Unity on builds. `revert_noise` (run by `build_server`) reverts it; don't commit that change.
