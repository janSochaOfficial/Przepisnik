# Coordinator runbook

The **coordinator** is the main Claude Code session. It doesn't write feature code. It starts task agents, merges their branches, keeps `main` green and makes the decisions agents escalate.
Rules that agents follow are in [IMPLEMENTATION_PLAN.md §4](IMPLEMENTATION_PLAN.md). This file is the coordinator's side.

## 0. Starting (or resuming) a coordinator session
1. Read `CLAUDE.md`, `PROJECT.md`, `TECHNICAL.md`, `docs/IMPLEMENTATION_PLAN.md`, and this file.
2. Rebuild the state from git, not from memory:
   - `git log --oneline main` and `git branch --list 'task/*'` show what is merged and what is in flight;
   - `docs/tasks/*.md` with a `## Report` section are tasks that finished (merged task files are removed afterwards);
   - the **status board** below is the summary. Update it whenever something changes (commit it on `main`).
3. Check the environment once per session: `dotnet --version` (10.x), `node --version` (24.x), `docker info` (works without sudo).

## 1. Status board

All v1 tasks (T00, B1–B8, S1, F0–F6, T90) were merged on 2026-10-24 and their worktrees and branches removed. The per-task notes are in git history (`git show 1200bc8:docs/COORDINATOR.md`) and summarised in [AGENT_DECISIONS.md](AGENT_DECISIONS.md).
For a new wave, add rows here:

### Wave v1.1 (notes.md, 2026-10-24)
Lane A: U1 → U2. Lane B: P1 → S2 → I1 (I1 also needs U1). G1 (Google sign-in) is done by the coordinator, and needs the user's OAuth client ID.

| Task | Slot | State | Branch | Notes |
|---|---|---|---|---|
| U1 | 1 | merged | task/U1-custom-units | merged 2026-10-24 + coordinator migration `U1_CustomUnits`. Agent gate green (337 unit, 362 integration, 556 vitest, 195 playwright); full verify on main was interrupted (session limit), rerun it. Accepted: ×0.6 shows `1 ¼ puszki` without `~` (4.2 % < 5 %, D-31); unknown `customUnitId` → 400 `validation.failed` / `unit.not_found`; Program.cs wired `AddUnits`/`MapUnits` |
| P1 | 2 | merged | task/P1-product-prompt-ranking | merged 2026-10-24 + coordinator migration `P1_ProductIsVerified` (seeded/admin products verified). Full verify green (298 unit, 335 integration, 507 vitest, 183 playwright). Accepted: `isVerified` added to the Recipe/Variant/Fridge product DTOs; prompt only in the recipe editor's `IngredientRow`; cancelling the new-product form keeps the text, so the prompt can repeat |
| S2 | 3 | merged | task/S2-optional-missing | merged 2026-10-24. Full verify green except 5 mobile Playwright timeouts at load ~15 (U1 building); those 2 specs rerun green with 2 workers. Accepted: own `OptionalPrompt.svelte` (AlertDialog; `ConfirmDialog` can't tell Escape from its cancel button); deduction switches still don't filter add-missing (as before); harness `?optional=1` fixture |
| I1 | 4 | todo | task/I1-import-as-variant | started, then stopped at the session limit before its first commit; restart from scratch (branch is empty, delete it first) |
| U2 | 5 | todo | task/U2-shopping-units | started, then stopped at the session limit; worktree `.claude/worktrees/agent-acc3815f31f528f4f` keeps a `wip:` commit (entity + config + UnitAdmin in-use check, unverified); resume from it |
| G1 | 0 | merged | (coordinator, on main) | Google sign-in confirmed by the user 2026-10-25 on `npm run dev` (Chrome). D-36 login page + docs (595fd2e); FedCM for the button (82175be) fixed the popup hanging on /gsi/transform. Client ID and admin email in git-ignored `.env.local` / `deploy/.env`; the Docker stack (:8080) is not tried yet |

States: `todo` → `running` → `review` (Report written) → `merged` (or `blocked` with a reason).

## 2. Loop
Repeat until every task is `merged`:

1. **Pick ready tasks**: `todo` tasks whose dependencies are all `merged` (plan §3). Start them in parallel, but no more than **2 at once** (user decision, 2026-10-24). Prefer the critical path (B3, B4, F0, F1, F2).
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
> **User instruction (2026-10-24): do not escalate for the rest of this session.** Decide yourself, ranking PROJECT.md first, then TECHNICAL.md, then the task files; record new functional decisions in PROJECT.md's decision log as `assumption` and note them on the board. Still never push, deploy, or touch non-local data.

Ask the user, and don't decide alone, when:
- a requested change contradicts PROJECT.md (a functional decision);
- two tasks' contract changes truly conflict;
- an agent fails the same gate twice after feedback;
- anything would push, deploy, or touch data outside the local machine.
