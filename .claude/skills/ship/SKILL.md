---
name: ship
description: Commit, push, open pull requests and merge for the MOBA project repos (moba-arpg-game, moba-arpg-backend, moba-arpg-ideas), following each repo's branch rules (work branch → PR into dev → release PR into main). Use when the user says "commit", "push", "push it", "push both", "open a PR", "merge", "release", "fetch main" or similar, and before any commit, to keep Unity's settings noise and test leftovers out.
---

# Ship: commit, push, PR, merge

The project has three public repos under `you-are-not-prepared`, checked out side by side in `C:\Project MOBA`:

| Repo | Folder | Flow |
|---|---|---|
| `moba-arpg-game` | Unity game | **work branch → PR into `dev` (squash) → release PR `dev` → `main`** |
| `moba-arpg-backend` | ASP.NET Core API + Postgres | the same |
| `moba-arpg-ideas` | Concepts, sketches, design notes | **straight to `main`**, no dev, no PRs |

The branch rules are in each repo's `CONTRIBUTING.md`; this skill is how to carry them out. **Never commit directly to `dev` or `main`** in moba-arpg-game and moba-arpg-backend.

## The user's words → what to do

- **"commit"**: commit on a work branch (start one from `dev` if you're on `dev`, see below). Don't push.
- **"push" / "push it"**: commit if needed, then push the work branch. No PR unless asked.
- **"open a PR"**: push, then open a PR from the work branch into `dev`.
- **"merge"**: squash merge the PR into `dev`, delete the branch, and bring the local checkout back to an up-to-date `dev`.
- **"push both"**: the same in moba-arpg-game **and** moba-arpg-backend, with the same branch name and one PR per repo.
- **"release" / "PR to main" / "merge to main"**: a PR from `dev` into `main`, merged with a merge commit.
- **"fetch main"**: fast-forward local `main` to `origin/main` and local `dev` to `origin/dev`.
- **Merge only when the user says so.** Never push or merge on your own initiative. Never force-push, never rewrite pushed history, never skip hooks.
- **No version tags for now.** CONTRIBUTING.md describes `vX.Y.Z` tags on releases, but the user will add a changelog first. Don't tag, and don't add version numbers anywhere.

## Before committing

1. **Right branch:** `git branch --show-current` must be a work branch, never `dev` or `main`.
   - On `dev` with uncommitted changes: `git fetch origin`, check `dev` isn't behind `origin/dev` (`git status -sb`; if it is, `git pull --ff-only` first), then `git switch -c <type>/<short-name>`. The changes come along.
   - On `main`: don't commit. Switch to `dev` (the changes come along), then start the work branch as above. Only a `hotfix/*` branch starts from `main`.
2. **Name it** `type/short-name`: lowercase, words separated by dashes, plus the issue number if there is one (`fix/42-ascension-level-check`).

   | Prefix | For | Repos |
   |---|---|---|
   | `feature/` | New systems, engine work, endpoints | both |
   | `content/` | Heroes, spells, mods, maps, objectives | game |
   | `fix/` | Bug fixes | both |
   | `balance/` | Number changes only | game |
   | `chore/` | Upgrades, cleanup, tooling, docs | both |
   | `hotfix/` | Urgent fix to `main` (maintainers only) | both |

3. **Look at everything staged:** `git status --short` and `git diff --stat`. Remove noise:
   - `ProjectSettings/UnityConnectSettings.asset` with `m_Enabled 0 → 1`: Unity flips it by itself. Revert it (`git checkout -- ProjectSettings/UnityConnectSettings.asset`).
   - `ProjectSettings/ProjectSettings.asset` with `insecureHttpOption: 2`: left over from a server build. Revert it.
   - `Assets/Temp/` (test screenshots), `*.html` render leftovers in moba-arpg-ideas.
   - **Keep** changes Unity makes as a real consequence of the work: regenerated scenes, prefabs and hero assets after a rebuild, `Packages/packages-lock.json` with a manifest change, new `.meta` files.
4. **No secrets:** no `.env`, no real keys or passwords. Local-only defaults (`local-dev-only`, `local-dev-only-server-key`) are fine.
5. **Docs follow the change:** if behaviour, settings, endpoints or setup changed, update the repo's `README.md` in the same commit. That covers the moba-arpg-game status line, structure and game-server sections, and the moba-arpg-backend API table and recording rules.

## Commit message

- **Subject:** one line, under about 72 characters, saying what changed for the game ("Game server stage 2: spells, shop, inventory and ascension via the server"). If the task has an issue, start with its number: `#42 Add Thessa's passive`.
- **Body:** a short "why", then bullets of the notable changes. Mention what was tested only if it isn't obvious.
- **Ending:** the attribution lines the session asks for (currently `Co-Authored-By: Claude ... <noreply@anthropic.com>`).
- **Multi-line messages:** pass them with a quoted heredoc so nothing gets expanded:
  ```bash
  git commit -F - <<'EOF'
  Subject line

  Body...

  Co-Authored-By: ...
  EOF
  ```

## Pull request into dev

```bash
git push -u origin <branch>
gh pr create --repo you-are-not-prepared/<repo> --base dev --head <branch> --title "<subject>" --body "$(cat <<'EOF'
## Summary
- what changed and why (bullets; bold lead-ins for groups)

## Not in this PR
- follow-ups, if any

## Testing
- what was run and what it showed (Editor tests, Docker server, curl...)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- In moba-arpg-game, CONTRIBUTING asks the PR to say **which scenes and prefabs it touched**.
- Before opening it, the branch should have the latest `dev`: if `origin/dev` moved on, `git merge origin/dev` into the branch first.

**Merging into dev** (only when the user said to merge):
```bash
gh pr merge <N> --repo you-are-not-prepared/<repo> --squash --delete-branch
git switch dev && git pull --ff-only origin dev && git fetch --prune
git diff --quiet <branch> dev && git branch -D <branch>     # squash merges need -D; the diff check proves dev has it all
```
- `gh pr merge --repo …` run outside the checkout doesn't touch the local branches, hence the last two lines.
- `dev` has no ruleset, so no `--admin` is needed. CONTRIBUTING says a maintainer reviews PRs into `dev`: if you aren't tomicnikola (`gh api user --jq .login`), open the PR and leave the merge to a maintainer.

## Release: dev → main

```bash
gh pr create --repo you-are-not-prepared/<repo> --base main --head dev --title "<what this release brings>" --body "..."
gh pr merge <N> --repo you-are-not-prepared/<repo> --merge --admin     # only when the user said to merge, and only as tomicnikola
```

- `main` in all three repos has the "Protect main" ruleset: no deletion, no force-push, PRs need 1 approval. Only tomicnikola (org admin) can bypass it:
  - **tomicnikola:** GitHub doesn't allow approving your own PR, so merge with `--admin`. Pushes straight to moba-arpg-ideas `main` go through as a bypass.
  - **Anyone else:** never use `--admin`. Open the PR and leave the approval and merge to tomicnikola. Changes to moba-arpg-ideas also go through a PR.
- Always a merge commit, never squash, so `main` keeps the individual `dev` commits. No tag for now (see above).
- **Hotfix:** a `hotfix/*` branch from `main`, PR into `main`, then merge `main` back into `dev` right away so the fix isn't lost.

## Both repos

- When a change spans moba-arpg-game and moba-arpg-backend, use the same branch name in both, link the PRs to each other ("Goes with you-are-not-prepared/moba-arpg-backend#N"), and merge the backend PR first.
- Refer to PRs as `you-are-not-prepared/<repo>#N` or full links, never a bare `#N`.
- After merging, tell the user the PR link(s) and where the change is now (`dev` or `main`).

## moba-arpg-ideas

Commit and push straight to `main`: `git add -A && git commit ... && git push origin main`. No PRs. Don't commit generated `*.html` files or `.edge-profile/`; its `.gitignore` covers them.
