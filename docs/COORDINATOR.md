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
| T00 | 0 | merged | task/T00-foundation | merged 2026-10-24, full verify green (97 backend, 28 vitest, 6 playwright). Tests run on VSTest (MTP cannot run `dotnet test backend`); agent filter `--filter "Category!=Migrations"` |
| B1 | 1 | merged | task/B1-auth-users | merged 2026-10-24, full verify green (260 unit, 249 integration). Accepted: additive `IFridgeService.CreateOwnedAsync` in B5's file; `isAdmin` only for a Google-verified email; blank/too-long nickname → 400 `nickname.invalid`; diacritic-insensitive uniqueness (recorded as D-29); `defaultFridgeId: null` clears the default; own nickname reported available. Follow-ups: first login saves user, fridge and default in three steps without one transaction; a losing concurrent first login can briefly return `defaultFridgeId: null` |
| B2 | 2 | running | task/B2-catalog | |
| B3 | 3 | merged | task/B3-recipes-images | merged 2026-10-24 (+ coordinator: 400 for invalid image `size` documented). User decided D-27: archived recipes are read-only (PUT and PUT /visibility → 409 `recipe.archived`, `canEdit` false); fixed on main. Follow-ups: B4 `DropStalePicksAsync` could take a set of ids (B3 uses TransactionScope + two saves); recipe/variant DTO duplication (`RecipeProductRefDto`/`VariantProductRef`, `UserRefDto`/`VariantUserRef`) |
| B4 | 4 | merged | task/B4-variants | merged 2026-10-24. Contract: optional `Slot.afterSlotKey` (vector schema updated on main). Follow-up: concurrent duplicate variant name gives 500 not 409 |
| B5 | 5 | merged | task/B5-fridges | merged 2026-10-24 + coordinator migration `B5_FridgeOwnerNotUnique` (T00 model had a unique `ix_fridges_owner_id`). Full verify green. Cook counts on the versioned rows gave the author 409 on a concurrent cook: user chose D-28 → task B8. Follow-ups: concurrent duplicate invite `accept` / undo re-insert → 500 not 409; deductions never pruned |
| S1 | 8 | merged | task/S1-seed-data | merged 2026-10-24: 249 products, 35 tags, 50 utensils; `node shared/seed/validate.mjs` (no deps, mirrors `NameNormalizer`). `pieceForms` keys `one/few/many/fraction`. T90: wire the validator into `verify.sh` |
| F0 | 10 | merged | task/F0-design-system | merged 2026-10-24. Direction "Bolesławiec", see frontend/DESIGN.md; showcase at /ui (dev or VITE_UI_SHOWCASE=1) |
| F1 | 11 | merged | task/F1-quantities-lib | merged 2026-10-24. Coordinator: delete T00's duplicate `frontend/src/lib/domain/stubs.test.ts` |
| F2 | 12 | merged | task/F2-matching-lib | merged 2026-10-24. Requests relayed to B4: optional `Slot.afterSlotKey`; unknown step ids in a draft = new steps |
| B6 | 6 | todo | | needs B3, B4 (merged). Import can reuse `RecipeService.CreateAsync` |
| B7 | 7 | running | task/B7-favorites | needs B3, B4 (merged). Reuse `VariantQueries.ToSummaryRows` and `VariantSummaryResponse.From` for variant entries in `/favorites`. Repoint the two `AuthFoundationTests` that use `GET /favorites` as a 501 stub once favourites are real |
| F3 | 13 | todo | | needs F0 (merged). Rebuild `/logowanie` and `/witaj` with F0 components, then drop legacy colour names (`text`, `accent`, `border`) from app.css; toasts on those pages sit above a missing tab bar F4: link the profile import to `/przepisy?import=1`. F6 already built `/zaproszenie/[token]` (F3-owned path): F3 takes it over, adds the login round-trip and the expired empty state, and keeps F6's join flow. F6 added a `GET /fridges` mock to `smoke.spec.ts`. B1 merged: `PUT /me/nickname` re-issues the cookie (onboarding gate passes without a reload); nickname rules in `Core/Users/NicknameRules.cs` (NFC, 3–30, letters/digits/`_`/`-`, diacritic-insensitive uniqueness D-29). |
| F4 | 14 | merged | task/F4-recipe-catalog-editor | merged 2026-10-24, full verify green (427 vitest, 74 playwright). Edit page hosts "Zmień widoczność" / "Usuń przepis" until F5 has a menu. Inline new-product form lacks `pieceForms`. No dropdown primitive built |
| F5 | 15 | merged | task/F5-recipe-view-variants | merged 2026-10-24, full verify green (444 vitest, 103 playwright). Custom mix: nothing dimmed, ordered by `supportCount`. Own `draftFromSelection` (F2 `draftFromMatch` returns ids only), seed via `App.PageState.variantDraft`. No variants on archived recipes (matches backend). Recipe menu is a `BottomSheet`. Hands `CookActions.svelte` + `getCookContext()` to F6 |
| F6 | 16 | merged | task/F6-fridge-shopping | merged 2026-10-24 + coordinator `fix(merge)`: B5 `complete` returned created items in random order (batch shares `CreatedAt`); now shopping-list order. Full verify green after a load-induced timeout rerun (467 vitest, 135 playwright). F6 wired F5's `CookActions.svelte` (allowed). Deviations accepted: amounts in base units; item edit saves with a button; delete-undo re-creates under a new id; a shopping amount can't be cleared (API PATCH treats null as unchanged) |
| B8 | 9 | merged | task/B8-cook-counts | merged 2026-10-24 + coordinator migration `B8_CookCountTables` (create tables → copy non-zero counts → drop columns; Down copies back). Full verify green (Playwright needed a rerun: axe timeouts at load 19.8). Clock skew: the system clock ran ~1.7 h ahead earlier, so B5's migration ID is "future"; B8's ID was set to sort after it, and stale `bin/obj` made `dotnet ef` produce an empty migration until a `--no-incremental` build. Recipe detail and resolved variant +1 small query each |
| T90 | 0 | todo | | needs all. F3–F6 start every screen with `<Page>` (DESIGN.md §10–12); `PagePlaceholder` has no `Page` wrapper (replace or wrap). No dropdown-menu primitive yet. Wire `verify.sh --e2e` (T00 left a placeholder); document `dotnet run --no-build` for a second API from the same checkout T00 harness: add `ApiClient.PatchJsonAsync` (B5 has a local helper in `FridgeTestBase`). `shared/samples/export-v1.json` is missing (F4 import test uses a local fixture). F6 dev-only harness `/lodowka/harness-odejmowanie` (gated like `/ui`) may be dropped now that the recipe view hosts the deduction dialog. Shopping PATCH can't clear an amount (null = unchanged): consider an explicit clear. Verify under load: Vitest 5 s and axe timeouts at load ≥ 15, rerun before investigating. Coordinator made the showcase overlay axe test `test.slow()` (timed out in full parallel runs). |

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
