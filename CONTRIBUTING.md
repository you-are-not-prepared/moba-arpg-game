# Contributing

How we use issues, branches and pull requests in this repo. For setting up the project, see the [README](README.md).

The backend repo ([you-are-not-prepared/moba-arpg-backend](https://github.com/you-are-not-prepared/moba-arpg-backend)) follows the same rules.

---

## Branches

| Branch | What it is | How changes get in |
|---|---|---|
| `main` | Stable and playable. Every merge is a playtest build. | PR from `dev` (or a `hotfix/*` branch) |
| `dev` | Where finished work comes together. | PR from a work branch |
| work branches | One task each, short-lived (days, not weeks). | You push to them directly |

Never commit directly to `main` or `dev`.

## Issues and the project board

All issues in the game, backend and ideas repos are tracked on the org project board **[moba-arpg](https://github.com/orgs/you-are-not-prepared/projects/1)**. Each issue has one status:

| Status | Meaning | Who moves it there |
|---|---|---|
| **Backlog** | Logged, but nobody has agreed to do it yet. | Automatic when the issue is added |
| **Ready** | Agreed on and clear enough to start. Nobody is on it yet. | A maintainer |
| **In progress** | Someone is working on it, including while its PR into `dev` is open. | You, when you assign yourself and start the branch |
| **Ready for QA** | Merged into `dev`, waiting to be tested in a build. | Whoever merges the PR |
| **Done** | Tested and released to `main`. | Automatic when the issue closes |
| **Cancelled** | Closed without being done: a duplicate, not planned or invalid. | A maintainer, closing it as "not planned" |

Use the **`blocked`** label (not a status) when an issue is waiting on something else. Say in a comment what it's waiting on, and remove the label once it can continue.

Issues don't close by themselves when a PR is merged into `dev`, because GitHub only closes them from PRs into `main`. They close at release instead (see [Releases](#releases)). If an issue fails QA, move it back to **In progress** and fix it on a new branch.

## Naming work branches

`type/<issue>-short-name`: lowercase, words separated by dashes, with the issue number right after the prefix.

| Prefix | For | Example |
|---|---|---|
| `feature/` | New systems and engine work | `feature/12-server-address-field` |
| `content/` | Heroes, spells, mods, maps, objectives | `content/14-hero-thessa`, `content/15-map-delve` |
| `fix/` | Bug fixes | `fix/42-ascension-level-check` |
| `balance/` | Number changes only | `balance/51-q-spell-damage` |
| `chore/` | Upgrades, cleanup, tooling | `chore/23-upgrade-ngo` |
| `hotfix/` | Urgent fix to `main` (maintainers only) | `hotfix/60-login-crash` |

## Workflow

1. Pick an issue in **Ready**, assign yourself, and move it to **In progress**. Start your branch from an up-to-date `dev`.
2. Commit as often as you like and push your branch. **Start every commit message with the issue number:** `#42 Add Thessa's passive`. Every task needs an issue, so if there isn't one yet, create it first.
3. Before opening a PR, merge the latest `dev` into your branch.
4. Open a PR into `dev`. **Start its title with the issue number too** (`#42 Add Thessa's passive`): the squash commit on `dev` takes its message from the title. In the description, say what changed, how you tested it, and **which scenes and prefabs you touched**. Don't write `Closes #42`: it does nothing in a PR into `dev`.
5. A maintainer reviews it. Once it's approved, it is **squash merged**: the whole branch becomes one commit on `dev`. The branch is then deleted, and the issue moves to **Ready for QA**.

### Releases

When `dev` is playable, a maintainer opens a PR from `dev` into `main` and merges it with a **merge commit**. The merge on `main` is tagged (`v0.4.0`, `v0.5.0`, …), and that tag is the playtest and game server build.

In the release PR's description, list each issue in **Ready for QA** that passed testing as `Closes #42`. Merging the release closes those issues, and the board moves them to **Done**. Issues that failed go back to **In progress** and wait for the next release.

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
