---
name: ship
description: Commit, push, open pull requests and merge for the MOBA project repos (moba-test, moba-backend, moba-ideas), following each repo's branch rules. Use when the user says "commit", "push", "push it", "push both", "open a PR", "merge", "fetch main" or similar, and before any commit, to keep Unity's settings noise and test leftovers out.
---

# Ship: commit, push, PR, merge

The project has three repos, all private, under `tomicnikola`, checked out side by side in `C:\Project MOBA`:

| Repo | Folder | Flow |
|---|---|---|
| `moba-test` | Unity game | **dev → PR → main** |
| `moba-backend` | ASP.NET Core API + Postgres | **dev → PR → main** |
| `moba-ideas` | Concepts, sketches, design notes | **straight to `main`**, no dev, no PRs |

## The user's words → what to do

- **"commit"**: commit on `dev` (on `main` for moba-ideas). Don't push.
- **"push" / "push it"**: commit if needed, then push `dev`. No PR unless asked.
- **"push it, open a PR and merge"**: push `dev`, open a PR `dev` → `main`, merge it (merge commit).
- **"push both"**: the same in moba-test **and** moba-backend, with one PR per repo.
- **"open a PR but don't merge"**: stop after the PR.
- **"fetch main"**: fast-forward local `main` and `dev` to `origin/main`.
- **Merge only when the user says so.** Never push or merge on your own initiative. Never force-push, never rewrite pushed history, never skip hooks.

## Before committing

1. **Right branch:** `git branch --show-current` must be `dev` (moba-test, moba-backend). If you're on `main`, switch to `dev` and bring the changes along.
2. **Look at everything staged:** `git status --short` and `git diff --stat`. Remove noise:
   - `ProjectSettings/UnityConnectSettings.asset` with `m_Enabled 0 → 1`: Unity flips it by itself. Revert it (`git checkout -- ProjectSettings/UnityConnectSettings.asset`).
   - `ProjectSettings/ProjectSettings.asset` with `insecureHttpOption: 2`: left over from a server build. Revert it.
   - `Assets/Temp/` (test screenshots), `*.html` render leftovers in moba-ideas.
   - **Keep** changes Unity makes as a real consequence of the work: regenerated scenes, prefabs and hero assets after a rebuild, `Packages/packages-lock.json` with a manifest change, new `.meta` files.
3. **No secrets:** no `.env`, no real keys or passwords. Local-only defaults (`local-dev-only`, `local-dev-only-server-key`) are fine.
4. **Docs follow the change:** if behaviour, settings, endpoints or setup changed, update the repo's `README.md` in the same commit. That covers the moba-test status line, structure and game-server sections, and the moba-backend API table and recording rules.

## Commit message

- **Subject:** one line, under about 72 characters, saying what changed for the game ("Game server stage 2: spells, shop, inventory and ascension via the server").
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

## Pull request

```bash
git push origin dev
gh pr create --repo tomicnikola/<repo> --base main --head dev --title "<subject>" --body "$(cat <<'EOF'
## Summary
- what changed and why (bullets; bold lead-ins for groups)

## Not in this PR
- follow-ups, if any

## Testing
- what was run and what it showed (Editor tests, Docker server, curl...)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
gh pr merge dev --repo tomicnikola/<repo> --merge     # only when the user said to merge
gh pr list --repo tomicnikola/<repo> --state merged --limit 1
```

- When a change spans moba-test and moba-backend, say so in both PRs ("Goes with tomicnikola/moba-backend (…)").
- Refer to PRs as `tomicnikola/<repo>#N` or full links, never a bare `#N`.
- After merging, tell the user the PR link(s) and that it's on `main`. Keep `dev` as is: the next commit continues on it, and "fetch main" syncs it when asked.

## moba-ideas

Commit and push straight to `main`: `git add -A && git commit ... && git push origin main`. No PRs. Don't commit generated `*.html` files or `.edge-profile/`; its `.gitignore` covers them.
