# Coordinator runbook

The **coordinator** is the main Claude Code session. It doesn't write feature code. It starts task agents, merges their branches, keeps `main` green and makes the decisions agents escalate.
Rules that agents follow are in [IMPLEMENTATION_PLAN.md §4](IMPLEMENTATION_PLAN.md). This file is the coordinator's side.

## 0. Starting (or resuming) a coordinator session
1. Read `CLAUDE.md`, `PROJECT.md`, `TECHNICAL.md`, `docs/IMPLEMENTATION_PLAN.md`, and this file.
2. Rebuild the state from git, not from memory:
   - `git log --oneline main` and `git branch --list 'task/*'` show what is merged and what is in flight;
   - `docs/tasks/*.md` with a `## Report` section are tasks that finished;
   - the **status board** below is the summary. Update it whenever something changes (commit it on `main`).
3. Check the environment once per session: `dotnet --version` (10.x), `node --version` (24.x), `docker info` (works without sudo).

## 1. Status board

| Task | Slot | State | Branch | Notes |
|---|---|---|---|---|
| T00 | 0 | blocked | task/T00-foundation | paused 2026-10-23 (token budget): 6 commits on branch, 8 uncommitted files in worktree .claude/worktrees/agent-a21934c230a63d13f; was writing the integration test harness; no Report, verify.sh not run |
| B1 | 1 | todo | | |
| B2 | 2 | todo | | |
| B3 | 3 | todo | | |
| B4 | 4 | todo | | |
| B5 | 5 | todo | | |
| S1 | 8 | todo | | |
| F0 | 10 | todo | | |
| F1 | 11 | todo | | |
| F2 | 12 | todo | | |
| B6 | 6 | todo | | needs B3, B4 |
| B7 | 7 | todo | | needs B3, B4 |
| F3 | 13 | todo | | needs F0 |
| F4 | 14 | todo | | needs F0, F1 |
| F5 | 15 | todo | | needs F0, F1, F2 |
| F6 | 16 | todo | | needs F0, F1 |
| T90 | 0 | todo | | needs all |

States: `todo` → `running` → `review` (Report written) → `merged` (or `blocked` with a reason).

## 2. Loop
Repeat until every task is `merged`:

1. **Pick ready tasks**: `todo` tasks whose dependencies are all `merged` (plan §3). Start them in parallel, but no more than **5 at once** to keep the machine and the review load manageable. Prefer the critical path (B3, B4, F0, F1, F2).
2. **Spawn** each one with the Agent tool, `isolation: "worktree"`, and the prompt in §3. Set it to `running`.
3. **When an agent finishes**, read its Report and go through it:
   - *Model changes / Contract changes / Dependencies added*: check that they are justified and within the agent's own scope;
   - *Requests for other tasks*: turn them into notes on the affected task (or a small follow-up task) before that task starts;
   - *Deviations*: accept them, or send the agent back (SendMessage to the same agent) with a concrete fix request.
4. **Merge** it (§4). Set it to `merged`, update the board, and go back to step 1.

## 3. Spawn prompt

> You are implementing task `<ID>` of the RecipesList project, port slot `<n>`.
> First create `.env.local` in your worktree with `API_PORT=<5100+n>`, `WEB_PORT=<5200+n>` and `PREVIEW_PORT=<5300+n>`, and export them in every shell command that starts a server or Playwright.
> Read `CLAUDE.md`, then `docs/tasks/<file>.md`, and every document it lists under "Read first". Follow `docs/IMPLEMENTATION_PLAN.md §4` exactly: file ownership, no migrations, test vectors in your own file, ports, and dependencies as separate `deps:` commits.
> Work on branch `task/<ID>-<slug>`. Commit in small Conventional Commits.
> When the acceptance criteria are met: `git fetch && git rebase main`, then run `scripts/verify.sh --agent` until it is green, append `## Report` (sections from plan §4.7) to the task file, commit, and stop. Do not merge.
> If something in the specs is contradictory or missing, do not guess silently. Choose the most conservative option, mark it in the Report under *Deviations*, and continue.

## 4. Merge procedure (one task at a time)
```bash
git checkout main && git pull --ff-only                  # no-op locally, keeps the habit for when there is a remote
git merge --no-ff task/<ID>-<slug> -m "merge: <ID> <title>"
```
Resolve conflicts with these rules:

| File | Resolution |
|---|---|
| `package-lock.json` | Take `main`'s version, keep both sides' additions in `package.json`, then run `npm install` to regenerate |
| `Directory.Packages.props` | Keep both sides' `PackageVersion` lines; if two versions of the same package clash, use the higher one |
| `schema.d.ts` | Take either side, then `npm run gen:api` |
| `openapi.yaml` / `API.md` | Both sides should be inside different feature sections; merge by hand. If they truly overlap, decide, and note it in the decision log |
| Entity configurations / `AppDbContext` | Keep both additive changes |
| Anything an agent did not own | Prefer `main`, and re-apply the agent's intent by hand if needed (note it on the board) |

Then:
1. If the merge brought **model changes**: `dotnet ef migrations add <ID>_<Short> --project backend/src/RecipesList.Infrastructure --startup-project backend/src/RecipesList.Api`, review the migration, and commit it.
2. If it brought **contract changes**: `npm run gen:api` and commit `schema.d.ts`.
3. Run **`scripts/verify.sh`** (the full mode, including the Migrations trait).
   - Green → commit the merge and move on.
   - Red because of the merge → fix it on `main` in a `fix(merge): …` commit if it's small (≤ ~30 lines); otherwise `git merge --abort` and send the agent back with the failure output.
4. Tell the still-running agents that `main` moved only if the merge touched files they depend on (SendMessage: "main now contains <ID>; rebase before finishing").

## 5. Escalate to the user
Ask the user, and don't decide alone, when:
- a requested change contradicts PROJECT.md (a functional decision);
- two tasks' contract changes truly conflict;
- an agent fails the same gate twice after feedback;
- anything would push, deploy, or touch data outside the local machine.
