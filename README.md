# Project MOBA (moba-test)

A MOBA with ARPG builds, made in Unity. It has classic MOBA structure (lanes, creeps, towers, destroy the enemy core) with Path of Exile–style buildcrafting: heroes buy spells in the shop, put them on Q/W/E, and socket mods that change how they behave.

**Status:** early prototype.
- **Playable:** PvE (3v3 against bots), 5 heroes, 3 map layouts, accounts with match history.
- **Not yet built:** online PvP. Picking PvP in the menu starts a match with no other players.

The backend (accounts, settings sync, match history) lives in a separate repo: [tomicnikola/moba-backend](https://github.com/tomicnikola/moba-backend).

---

## Getting started

**Requirements**
- **Unity 6000.3.26f1** (Unity 6.3 LTS) with the Windows build support module.
- **Git LFS:** binary assets are stored with LFS. Run `git lfs install` once before cloning.
- *Optional:* the backend, for logging in. Without it, choose **Play offline**.

```bash
git lfs install
git clone https://github.com/tomicnikola/moba-test.git
```

1. Open the folder in Unity Hub (Add → the cloned folder).
2. Open `Assets/_Project/Scenes/Frontend.unity`.
3. Press **Play**. Log in, create an account, or click **Play offline**.
4. **Find a match → PvE**, pick a hero and **Lock in**.

You can also open a map scene (`Map_Classic`, `Map_Zones`, `Map_Delve`) and press Play to jump straight into a match. It starts with only your hero (the roster's first) and no bots.

---

## The game

### Match flow
- **Start screen:** log in, play offline, see your match history.
- **Find a match:** choose **PvE** (3v3 against bots; your teammates are bots too) or **PvP** (no online play yet, so you play the map alone).
- **Champion select:**
  - Each visit rolls a random map.
  - Both teams' picks are visible, and bots lock in one at a time.
  - A hero already taken by a teammate can't be picked.
- **Win** by destroying the enemy core.
  - Each lane has two towers per team; a lane's inner tower is protected while its outer tower stands.
  - The core is protected while any lane's inner tower stands.
- **End screen:** **Play again** returns to champion select with the same mode and a new map.

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
| Restart (end screen) | R |
| Options / cancel | Esc |

**Quick cast**, for spells and for attack move, fires immediately at the cursor with no targeting step. Turn it on in Options.

---

## Accounts and the backend

While logged in:
- **Settings:** options and keybinds are saved to your account and restored on login, including on another PC.
- **Matches:** each match is recorded when it ends, with level, K/D/A, CS, damage and final build for every player.
- **History:** **Match history** on the start screen shows your stats and recent matches. Click a match for full details, and hover items, spells and mods for their descriptions.
- **Cheats:** matches where the cheat menu was used stay in the history but don't count towards your stats.

To run the backend locally, see [moba-backend](https://github.com/tomicnikola/moba-backend): Docker, then `docker compose up -d --build`.

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

## Testing helpers

- **Cheat menu** (in a match, right side under Options):
  - +1 level and +10000 gold;
  - no mana costs and no cooldowns;
  - game speed: 1x / 2x / 4x / 8x.

  Using any cheat marks the match.
- **Test prices:** while `TestPrices` is `true` in `Assets/_Project/Editor/GameBuilder.cs`, every item, spell and mod costs **1 gold**. Bots then buy everything at once and hit very hard, so PvE matches end quickly. Set it to `false` and rebuild for real prices.
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
    Online/     Backend client, accounts and settings sync, match recording and history
    Settings/   GameSettings (options) and GameInput (rebindable controls)
    *.cs        Core: Unit, Attacker, HeroController, CreepAI, TowerAI, FogOfWar, GameManager...
  Shaders/     Fog of war overlay, shield wall, ground ring
  UI/          UI Toolkit: HUD (+ inventory, cheats, scoreboard), options menu, frontend (+ history)
```

### Generated content: edit the builder, not the assets

Scenes, prefabs, hero and item assets and most materials are generated by code. After changing heroes, items, maps or prefabs in `GameBuilder.cs`, run **MOBA → Rebuild All Scenes** in the Unity menu.

Rebuilding overwrites the generated assets, so hand edits to them are lost. Asset GUIDs are kept, so references don't break.

---

## Workflow

- Work happens on `dev`. Changes reach `main` through a pull request from `dev`.
- The backend repo works the same way.
