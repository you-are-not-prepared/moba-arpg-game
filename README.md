# Project MOBA (moba-arpg-game)

A MOBA with ARPG builds, made in Unity. It has classic MOBA structure (lanes, creeps, towers, destroy the enemy core) with Path of Exile–style buildcrafting: heroes buy spells in the shop, put them on Q/W/E, and socket mods that change how they behave.

**Status:** early prototype.
- **Playable:** PvE (3v3 against bots), 5 heroes, 3 map layouts, accounts with match history.
- **Game server (stage 4b):** PvP finds a match on a local game server in Docker. It has a queue and a shared champion select, runs the whole match, and records it on your account (see below).

The backend (accounts, settings sync, match history) lives in a separate repo: [you-are-not-prepared/moba-arpg-backend](https://github.com/you-are-not-prepared/moba-arpg-backend).

---

## Getting started

**Requirements**
- **Unity 6000.3.26f1** (Unity 6.3 LTS) with the Windows build support module.
- **Git LFS:** binary assets are stored with LFS. Run `git lfs install` once before cloning.
- *Optional:* the backend, for logging in. Without it, choose **Play offline**.

```bash
git lfs install
git clone https://github.com/you-are-not-prepared/moba-arpg-game.git
```

1. Open the folder in Unity Hub (Add → the cloned folder).
2. Open `Assets/_Project/Scenes/Frontend.unity`.
3. Press **Play**. Log in, create an account, or click **Play offline**.
4. **Find a match → PvE**, pick a hero and **Lock in**.

You can also open a map scene (`Map_Classic`, `Map_Zones`, `Map_Delve`) and press Play to jump straight into a match. It starts with only your hero (the roster's first) and no bots.

### Running the backend and game server (Docker)

PvE and **Play offline** need neither. Logging in needs the backend, and **PvP** needs the game server (plus the backend to record matches).

**Requirements**
- [Docker Desktop](https://www.docker.com/products/docker-desktop/), running.
- For the game server: the **Linux Dedicated Server Build Support** module for Unity 6000.3.26f1 (Unity Hub → Installs → Add modules).
- Free ports: 8080 (API), 5432 (database), 7777/UDP (game server). All three only listen on `127.0.0.1`.

**1. Clone both repos into the same folder.** The VS Code tasks find the backend at `../moba-arpg-backend`.
```
Project MOBA/
  moba-arpg-game/
  moba-arpg-backend/
```
```bash
git clone https://github.com/you-are-not-prepared/moba-arpg-backend.git
```

**2. Build the game server in Unity: MOBA → Build Linux Server.** The Docker image is made from `Builds/LinuxServer`, which git ignores, so every clone needs its own build. Redo it after **Rebuild All Scenes** or changes to code the server runs, or the game won't match the server.

**3. Start everything.** In a terminal in this folder (Visual Studio: View → Terminal; Rider and VS Code have one too):
```bash
.\moba start
```
This starts the backend, then the game server. The first start takes a few minutes while Docker downloads and builds the images. Your database starts empty, so create an account on the game's login screen.

**The `moba` script** (`moba.cmd`, which runs `scripts/moba.ps1`)
| Command | Does |
|---|---|
| `.\moba start` | Backend, then game server. Rebuilds the images, so run it again after pulling changes or making a new server build |
| `.\moba stop` | Stops both (the data is kept) |
| `.\moba logs server` / `.\moba logs backend` | Follows a log (Ctrl+C stops following, not the server) |
| `.\moba status` | Shows what's running |

- Add `backend` or `server` to *start* and *stop* for just one, e.g. `.\moba start server`.
- Before starting anything, it checks that Docker is running, the backend is cloned next to this repo, and the server build exists.
- In `cmd` it's `moba start`, without `.\`. On Mac/Linux, install PowerShell 7 and run `pwsh scripts/moba.ps1 start`.
- **VS Code:** the same commands are tasks (Terminal → Run Task…: *Start all*, *Stop all*, *Status*, and *start/stop/logs* for the backend and the game server).

---

## The game

### Match flow
- **Start screen:** log in, play offline, see your match history.
- **Find a match:** choose **PvE** (3v3 against bots; your teammates are bots too) or **PvP** (find a match on the local game server: a short search, then champion select with the other players; see [Game server](#game-server-local-docker)).
- **Champion select:**
  - Each visit rolls a random map.
  - Both teams' picks are visible, and bots lock in one at a time.
  - A hero already taken by a teammate can't be picked.
- **Win** by destroying the enemy core.
  - Each lane has two towers per team; a lane's inner tower is protected while its outer tower stands.
  - The core is protected while any lane's inner tower stands.
- **End screen:** **Continue** (or R) opens the **match analysis**: both teams with K/D/A, CS, damage, spells, mods and items, the same view as match history, offline too. **Back to menu** returns to the start screen.

### Maps
| Layout | Lanes | Notes |
|---|---|---|
| **Classic** | 3 (top, mid, bottom) | Jungle of rocks between the lanes |
| **Zones** | 2 | Lanes bend through four zones around an open centre (zone effects come later) |
| **Delve** | 1 | A single lane down a mine shaft and back up |

### Heroes
Every hero has a passive and an ultimate locked to **R**. Q/W/E are filled with spells you buy.

| Hero | Passive | Ultimate |
|---|---|---|
| **Kael**, Ranger | Projectile spells fire +1 projectile | **Skybreaker Arrow:** a huge arrow that pierces and stuns |
| **Vesh**, Chronomancer | 20% shorter cooldowns | **Rewind:** resets Q/W/E cooldowns |
| **Brann**, Artificer | Items give 10% more stats | **Overcharged Sockets:** a 4th mod socket on Q/W/E (passive once learned) |
| **Torvald**, Bulwark (melee) | +50 max HP per level, 15% slower | **Aegis Wall:** a moving shield that blocks enemy projectiles from the front for 5s (tower shots pass through) |
| **Nyx**, Stalker (melee) | +15% damage to targets with no allied hero within 7m | **Bloodrush:** +60% move speed and +20% attack speed for 7s, refreshed on hero kills |
| **Thessa**, Windrunner | Each spell cast gives +25% move speed for 2s | **Eye of the Storm:** for 6s (7/8 at higher ranks) spells cost no mana and have 50% shorter cooldowns |
| **Oren**, Ember Duelist (melee) | Attacks build Heat (5 max, +4% attack speed each); at 5 the next attack bursts for 50 fire damage around the target and burns | **Blazing Leap:** leaps up to 6m, 150/225/300 damage and a burn where he lands; his attacks burn for 5s |
| **Ilsa**, Frostwarden | Spell hits slow 20% for 1.5s; slowed enemies take 10% more damage from her | **Glacial Prison:** after 0.75s freezes enemies in a 3.5m area for 1.5–2s, then the ice slows (30%) for 3s |
| **Maela**, Grovekeeper | Spell hits heal the nearest allied hero (8m) for 15% of the damage | **Sanctuary:** a 5m grove for 6s: allied heroes heal 4–6% max health/s and move 20% faster, enemies are slowed 20% |
| **Corvin**, Stormcaller | Every 4th spell is charged: its first hit jumps to 1 more enemy for 50% damage | **Thunderhead:** a storm cloud follows him for 6s and strikes an enemy within 6m every 0.5s (heroes first) |

Building blocks the heroes added, ready for new spells and mods: **slows** (a status like stun and burn, shown as an icy border on health bars), **ground zones** (`ZoneSpell`: warning ring, freeze, slow, damage, ally healing and speed), a **leap** (`LeapSpell`), **chain lightning** on spell hits, timed buffs (`SpellStorm`, `StormCloud`, `BurningBlades`), and `SpellEvents.Hit` for passives that react to spell hits.

Melee heroes and creeps show each attack as a swing: the model lunges at the target while a slash arc sweeps across in front (`MeleeSwing`, shader `Moba/MeleeSlash`). On the game server, every player sees it.

### Ascension (levels 6, 11, 18)
At levels 6, 11 and 18 talent cards appear in the middle of the screen. The game keeps running, and you click one to choose.
- **Level 6** picks a path, one card per path: **A** builds on the hero's passive, **B** on the ultimate, and **C** is a hybrid playstyle that draws on both. The system supports 2–4 paths per hero.
- **Level 11** offers two talents from that path (e.g. A1/A2 or C1/C2), and **level 18** two more (A3/A4 or C3/C4).

Talents give stats (health, damage, attack and move speed, cooldowns, spell damage, projectiles, item stats). Some boost the hero's own kit, such as a longer Aegis Wall or Bloodrush, a longer Skybreaker stun, or more damage to isolated targets.

The current talents are placeholders; they're defined in `Assets/_Project/Editor/GameBuilder.Ascension.cs`. Bots pick at random. Your picks are listed in the portrait tooltip.

### Items, spells and mods
- **The shop** works only in your base, plus anywhere while you're dead. Selling refunds 70%. Open it with **B** or the Shop button.
- **The inventory** has 8 slots, rearranged by dragging. Items, spells and mods all take a slot until equipped.
- **Spells:** drag one from the inventory onto Q, W or E. Ranks belong to the slot, so swapping spells keeps the rank.
- **Mods:** drag one onto a spell to socket it. Each spell has 3 sockets; a mod only works if it suits the spell (e.g. *Volley* needs a projectile spell).
  - **Volley:** +2 projectiles.
  - **Fork:** splits on hit.
  - **Searing:** more damage, plus a burn.

### Economy and progression
- **Gold:** from last hits, tower kills and hero kills, plus 1 gold per second.
- **XP and levels:** XP comes from the kills you land (last hits on creeps, towers and heroes), up to level 18.
- **Ability ranks:** basic spells unlock ranks at levels 1/3/5/7/9; the ultimate at 6/11/16.
- **Fountain:** standing in your own base restores 10% of health and mana per second.
- **Respawn:** after 5 seconds.
- **Fog of war:** you see what your team sees. Enemy units in the fog are hidden.

### Scoreboard (hold Tab)
- One row per hero: level, K/D/A, CS, the four spells with their mod sockets, and the inventory.
- Team kill totals and the match time at the top.
- No gold, by design.
- **Assists:** you get one for damaging an enemy hero in the 10 seconds before a teammate kills them.

---

## Controls

Every control except Esc can be rebound in **Options → Controls**, which also holds the HUD size and quick-cast settings.

| Action | Default |
|---|---|
| Move / attack | Right mouse button |
| Confirm target | Left mouse button |
| Attack move | A |
| Stop | S |
| Abilities | Q, W, E, R |
| Level up an ability | Ctrl + ability key |
| Toggle camera lock | Y |
| Center camera on hero (hold) | Space |
| Pan camera | Arrow keys (or screen edges when unlocked) |
| Zoom | Mouse wheel |
| Open / close shop | B |
| Scoreboard (hold) | Tab |
| Continue (end screen) | R |
| Options / cancel | Esc |

**Quick cast**, for spells and for attack move, fires immediately at the cursor with no targeting step. Turn it on in Options.

---

## Accounts and the backend

While logged in:
- **Settings:** options and keybinds are saved to your account and restored on login, including on another PC.
- **Matches:** each match is recorded when it ends, with level, K/D/A, CS, damage and final build for every player.
- **History:** **Match history** on the start screen shows your stats and recent matches. Click a match for full details, and hover items, spells and mods for their descriptions.
- **Cheats:** matches where the cheat menu was used stay in the history but don't count towards your stats.

To run the backend locally, see [moba-arpg-backend](https://github.com/you-are-not-prepared/moba-arpg-backend): Docker, then `docker compose up -d --build`.

**Backend settings**
- **Server address:** the game connects to `http://localhost:8080`. To use another server, set the PlayerPrefs key `moba.backend.url`.
- **HTTP vs HTTPS:** plain `http://` is allowed in the Editor and development builds only. Release builds need an HTTPS server.

**Saved data (PlayerPrefs)**
| Key | Holds |
|---|---|
| `moba.settings` | Options (HUD sizes, quick cast) |
| `moba.input.bindingOverrides` | Rebound controls |
| `moba.auth.token` | The remembered login session |
| `moba.backend.url` | Backend address override (optional) |

---

## Game server (local, Docker)

The game can run as a **server that runs the whole match**: movement, creeps, towers, bots, combat and respawns. The game connects to it as a client: it sends your orders (move, attack, attack move, stop, casts, rank-ups) and choices (buying, selling, drag and drop, ascension), and shows what the server says is happening. It uses Unity Netcode for GameObjects; the server is a headless Linux build of this same project.

**Run it** (first-time setup: [Running the backend and game server](#running-the-backend-and-game-server-docker))
1. In Unity: **MOBA → Build Linux Server**. This needs the *Linux Dedicated Server Build Support* module and writes to `Builds/LinuxServer` (ignored by git).
2. In this folder: `.\moba start server` (or `docker compose up -d --build`).
3. In the game: **Find a match → PvP**. You wait briefly while the server gathers players, then pick a hero in champion select and **Lock in**.

Without the Editor open, you can also build from the command line:
```bash
unity build --target StandaloneLinux64 --execute-method Moba.EditorTools.GameBuilder.BuildLinuxServerBatch .
```

**How the server behaves**
- **Queue:** the first player starts a 15-second wait for more players ("Searching… · 2 players found"). It ends early when the match is full (6 players).
- **Champion select:**
  - Players are dealt onto both teams in the order they joined, and bots take the free seats (up to `MOBA_BOTS` per team) and pick a moment apart.
  - Everyone sees every pick live, and nobody can take a hero their own team already has.
  - It ends 5 seconds after everyone has locked in, when the 60-second timer runs out (random heroes for anyone still picking), or when a player presses **Start now**.
  - Someone joining now takes a free or bot seat; someone leaving gives their seat to a bot.
- **Match:** only then does the server load the map, on everyone's screen. A running match only takes back players who left it (logged in, same account); anyone else is told to try again when it ends.
- **Map:** with `MOBA_MAP: random`, the server rolls a map when it starts, and again whenever the queue empties. Champion select shows it.
- The match stays paused until the first player joins.
- **Leaving:** a player who leaves keeps their hero in the match, standing still. Logging back in to the same server hands it back.
- **Recording:** logged-in players are identified with the backend through a join ticket. When a core falls, the server records the match on everyone's account; it then shows in their match history and stats.
- **Next match:** the server quits 15 seconds after the match ends, and Docker restarts it with a fresh one. Players still on the end screen go straight to the match analysis.
- It listens on UDP 7777, bound to `127.0.0.1`, so only this PC can join.

**Settings** (in `docker-compose.yml`)
- `MOBA_MAP`: `classic`, `zones`, `delve` or `random`.
- `MOBA_BOTS`: bots per team for the seats players don't fill (0 = players only).
- `MOBA_PORT`: the UDP port.
- `MOBA_CHEATS`: `on` lets players use the cheat menu (without game speed). On in `docker-compose.yml` for local testing; set it to `off` before others play.
- `MOBA_BACKEND_URL` / `MOBA_BACKEND_KEY`: where to record matches, and the backend's game-server key.
  - The defaults match the local backend on this PC (moba-arpg-backend's `docker compose up`).
  - Without a key, nothing is recorded.
  - On a real server, set `GAME_SERVER_KEY` in a `.env` file here and in moba-arpg-backend (git ignores `.env`).

**Two players on one PC**
1. In Unity: **MOBA → Build Windows Client**. This writes `Builds/WindowsClient/MobaTest.exe` (ignored by git).
2. Start it as a second player. It looks for a match straight away, with no login (an anonymous player), and locks in the hero you name (or another one if its team already has it):
   ```bash
   Builds/WindowsClient/MobaTest.exe -screen-fullscreen 0 -screen-width 1280 -screen-height 720 -join vesh -name "Player 2"
   ```
   Without `-join`, it starts at the login screen like the normal game.
3. Within 15 seconds, search from the Editor (or another copy) as usual. Both players meet in the same champion select, on opposite teams.

**Useful commands**
- `docker compose logs -f game-server` follows the server log.
- `docker compose down` stops the server.
- PlayerPrefs `moba.gameserver.address` (`host:port`) points the game at a different server.

**What comes from the server**
- Stage 1: movement, attacks, creeps, towers, cores, health, gold, XP, levels, K/D/A, CS, deaths and respawns, the match result, and attack visuals.
- Stage 2:
  - spells and ultimates: casting (walking into range first), mana, cooldowns, ranks and skill points;
  - spell visuals: projectiles, explosions, the shield wall and the haste aura, plus stuns and burns;
  - the shop (buy and sell), the inventory and spell/mod drag and drop. The HUD shows a drag at once, and the server's result follows;
  - ascension choices;
  - every hero's kit, so the Tab scoreboard shows everyone's items and spells.
- Stage 3:
  - the match clock and the creep wave timer;
  - damage numbers, for the scoreboard and analysis;
  - match recording on the backend, as described above.

PvE stays fully offline as before.

**Important**
- **Re-run Build Linux Server after rebuilding the scenes:** in-scene objects are matched between server and client by IDs saved in the scenes.
- **Keep the Editor's build target on Windows:** if the Editor's build target is ever left on Linux Server, the Editor itself compiles as a server. The build helpers switch it back automatically.

## Releases and updates (Windows)

Players get the game once as a zip from the [releases page](https://github.com/you-are-not-prepared/moba-arpg-game/releases), extract it anywhere they can write to (not Program Files), and run `MobaTest.exe`. After that the game updates itself.

**What players see**
- The menu shows the game's version in the bottom-left corner.
- When a newer release is out, the login and start screens show a bar with the first lines of its notes, plus **Update now** and **Later**.
- **Update now** downloads the release and quits the game. A small window swaps the files and starts the new version.

**How it works**
- On every start (not in the Editor or on the game server), `GameUpdater` asks GitHub for the latest release.
- It only offers the release when its tag (`v0.2.0`) is newer than this build's **Player Settings → Version**.
- The update replaces the game's folders whole, so no old files stay behind. If it fails, it says so and links the releases page; the log is `%TEMP%\DefaultCompany\moba-test\update\install-update.log`.

**Publishing a release**
1. Merge what's going out into `main`.
2. In Unity, raise **Project Settings → Player → Version** (e.g. `0.1.0` → `0.2.0`), then **MOBA → Build Windows Client**. The build writes its version to `version.txt` next to the exe.
3. Publish it:
   ```bash
   .\moba release
   ```
   This zips `Builds/WindowsClient` as `MobaTest-Windows.zip` and creates the GitHub release `v<version>` on `main`. The notes list the PRs merged since the last release; use `-Notes "..."` to write your own. It refuses a version that's already released.

**Testing an update without publishing:** start a build with `-update-feed <url>`, where the URL serves the same JSON as GitHub's latest-release API (with a `MobaTest-Windows.zip` asset).

## Testing helpers

- **Cheat menu** (in a match, right side under Options):
  - +1 level and +10000 gold;
  - no mana costs and no cooldowns;
  - game speed: 1x / 2x / 4x / 8x.

  Using any cheat marks the match. On the game server the menu works when `MOBA_CHEATS` is `on`.
- **Test prices:** while `TestPrices` is `true` in `Assets/_Project/Editor/GameBuilder.cs`, every item, spell and mod costs **1 gold**.
- **Bot shopping:** for now, bots buy only one spell (the shop's first, Fireball) for Q: no items and no mods. Set `fullShopping` on `BotBrain` to bring back spells for every slot, mods and items. With 1-gold prices that means a full inventory at once, and matches end within minutes. The same goes for the bots on the game server. Set it to `false` and rebuild for real prices.
- **Test colours:** `TestKindColors` colours inventory tiles by kind (items orange, spells blue, mods green).

---

## Project structure

```
Assets/_Project/
  Editor/      GameBuilder: generates scenes, prefabs, heroes, items and materials (+ ascension talents)
  Heroes/      Hero definitions, the roster and ultimates (generated)
  Items/       Items, spells, mods and the shop catalog (generated)
  Materials/   (generated)
  Prefabs/     Hero and creep prefabs (generated)
  Scenes/      Frontend + one scene per map layout, each with a baked NavMesh (generated)
  Scripts/
    Abilities/  Spell framework: AbilityCaster, AbilityDefinition, spells, mods, projectiles
    Bots/       BotBrain (hero AI)
    Heroes/     Hero mechanics: ascension, Aegis Wall shield, Bloodrush haste, isolation passive
    Items/      Inventory, shop, item catalog, loadout (equip/swap rules)
    Match/      Heroes, roster, map layouts, MatchSettings, MatchBootstrap (spawns the lineup)
    Net/        Game server: NetSession, GameServer (server boot), GameClient (joining), NetworkUnit/Hero/Match, NetItems (item ids)
    Online/     Backend client, accounts and settings sync, match recording and history
    Settings/   GameSettings (options) and GameInput (rebindable controls)
    *.cs        Core: Unit, Attacker, HeroController, CreepAI, TowerAI, FogOfWar, GameManager...
  Resources/   NetworkPrefabs (generated): prefabs the game server spawns
  Shaders/     Fog of war overlay, shield wall, ground ring
  UI/          UI Toolkit: HUD (+ inventory, cheats, scoreboard), options menu, frontend (+ history)
```

### Generated content: edit the builder, not the assets

Scenes, prefabs, hero and item assets and most materials are generated by code. After changing heroes, items, maps or prefabs in `GameBuilder.cs`, run **MOBA → Rebuild All Scenes** in the Unity menu.

Rebuilding overwrites the generated assets, so hand edits to them are lost. Asset GUIDs are kept, so references don't break.

---

## Workflow

- Each task gets a work branch from `dev` (`feature/…`, `content/…`, `fix/…`, …), which goes into `dev` through a pull request. Changes reach `main` through a pull request from `dev`.
- The details (branch names, reviews, releases, hotfixes, avoiding scene conflicts) are in [CONTRIBUTING.md](CONTRIBUTING.md).
- The backend repo works the same way.
