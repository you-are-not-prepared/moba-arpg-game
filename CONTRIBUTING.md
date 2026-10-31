# Contributing

How we use branches and pull requests in this repo. For setting up the project, see the [README](README.md).

The backend repo ([you-are-not-prepared/moba-arpg-backend](https://github.com/you-are-not-prepared/moba-arpg-backend)) follows the same rules.

---

## Branches

| Branch | What it is | How changes get in |
|---|---|---|
| `main` | Stable and playable. Every merge is a playtest build. | PR from `dev` (or a `hotfix/*` branch) |
| `dev` | Where finished work comes together. | PR from a work branch |
| work branches | One task each, short-lived (days, not weeks). | You push to them directly |

Never commit directly to `main` or `dev`.

## Naming work branches

`type/short-name`, lowercase, words separated by dashes. Add the issue number if there is one.

| Prefix | For | Example |
|---|---|---|
| `feature/` | New systems and engine work | `feature/server-address-field` |
| `content/` | Heroes, spells, mods, maps, objectives | `content/hero-thessa`, `content/map-delve` |
| `fix/` | Bug fixes | `fix/ascension-level-check` |
| `balance/` | Number changes only | `balance/q-spell-damage` |
| `chore/` | Upgrades, cleanup, tooling | `chore/upgrade-ngo` |
| `hotfix/` | Urgent fix to `main` (maintainers only) | `hotfix/login-crash` |

## Workflow

1. Start from an up-to-date `dev`.
2. Commit as often as you like and push your branch. If the task has an issue, start each commit message with its number: `#42 Add Thessa's passive`.
3. Before opening a PR, merge the latest `dev` into your branch.
4. Open a PR into `dev`. In the description, say what changed, how you tested it, and **which scenes and prefabs you touched**.
5. A maintainer reviews it. Once it's approved, it is **squash merged**: the whole branch becomes one commit on `dev`. The branch is then deleted.

### Releases

When `dev` is playable, a maintainer opens a PR from `dev` into `main` and merges it with a **merge commit**. The merge on `main` is tagged (`v0.4.0`, `v0.5.0`, …), and that tag is the playtest and game server build.

### Versioning

Tags follow `vMAJOR.MINOR.PATCH`:

- **Major:** big or breaking changes, such as old clients no longer working with the server. While the game is in early development it stays at `0`.
- **Minor:** a release from `dev` with new features or content (`v0.4.0` → `v0.5.0`).
- **Patch:** bug fixes only, usually a hotfix (`v0.5.0` → `v0.5.1`).

When you bump a number, the numbers after it reset to 0.

### Hotfixes

Branch `hotfix/*` from `main`, open a PR into `main`, then merge `main` back into `dev` right away so the fix isn't lost.

## Unity: avoiding merge conflicts

Scenes and prefabs are the files that conflict most, and those conflicts are hard to resolve by hand.

- **One person per scene at a time.** Claim a scene in Discord before you edit it, and release it when your PR is merged.
- **Build in prefabs, not directly in scenes.** Two people rarely edit the same prefab, while everyone edits the map scenes.
- **Keep branches short.** A branch that touches a scene and lives for two weeks is very hard to merge.
- **Don't commit unrelated changes.** Unity re-saves files you only opened. Before committing, discard changes to scenes, prefabs and `ProjectSettings` that aren't part of your task.

### Set up Unity's merge tool (once per clone)

`.gitattributes` sends scenes, prefabs and other Unity YAML files to Unity's Smart Merge (`UnityYAMLMerge`). Git only uses it once you register it in your clone. Adjust the path if Unity is installed somewhere else:

```bash
git config merge.unityyamlmerge.name "Unity Smart Merge"
git config merge.unityyamlmerge.driver "'C:/Program Files/Unity/Hub/Editor/6000.3.26f1/Editor/Data/Tools/UnityYAMLMerge.exe' merge -p %O %B %A %A"
```

## Changes that touch both repos

Use the same branch name in both repos, open one PR in each and link them to each other. The backend PR is merged first.
