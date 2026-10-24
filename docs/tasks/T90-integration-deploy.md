# T90: Integration, deployment, E2E, hardening

**Wave 3 · depends on: all tasks merged**

## Goal
Turn the merged work into a coherent, deployable v1.

## Read first
Everything. All task Reports in `docs/tasks/*.md` (deviations, contract changes, follow-ups).

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `webapp-testing`.

## Owns
`deploy/**`, `backend/src/RecipesList.Api/Dockerfile`, `frontend/tests/e2e/**`, `README.md`, migrations, and any file needed to fix integration defects (note each in the Report).

## Deliverables
- Consolidate the model changes reported by the tasks into migrations; `MigrationsAreInSync` is green.
- Contract reconciliation: the backend's generated OpenAPI matches `openapi.yaml` (the contract test is green), `schema.d.ts` is regenerated, and the frontend type-checks.
- Replace any remaining harnesses or stubs (F5↔F6 `CookContext`, F4↔F5 `RecipeEditor`, the F3 import link).
- `deploy/compose.yaml` (db, api, web/Caddy), `Caddyfile` (static SPA fallback, `/api/*` proxy, gzip/zstd, security headers, the `SITE_ADDRESS` env), the API Dockerfile (multi-stage, non-root, health check), a frontend build stage producing static files for Caddy, `deploy/README.md` (first run, Google OAuth setup, backups, upgrades).
- E2E smoke suite (Playwright, against compose, dev-login enabled via `ASPNETCORE_ENVIRONMENT=Testing` in an override file): two users; A creates a **public** recipe with a photo; B adds a public variant and ♥ favourites the recipe (it shows in B's Ulubione); A can no longer edit the recipe directly and cannot narrow its visibility; A creates a second, **private** recipe, edits it once freely, adds a variant, edits it again → confirms the deletion of variants; A opens the recipe, picks B's variant option, scales, and deducts from a shared fridge (B invited A); the shopping list receives the missing items; export, then import as B.
- Hardening pass: query review with the `optimizing-ef-core-queries` skill over every service; check the rate limits; check that no email leaks in any response (an automated test that scans all integration-test responses for `@`); a check that no UI string is English (scan the built output for i18n keys or obvious English words in templates).

## Acceptance
- `docker compose -f deploy/compose.yaml up --build` from a clean clone serves the app on :8080, and the E2E suite passes against it.
- The full test matrix is green: `dotnet test` (incl. the Migrations trait), and `npm run check && npm run lint && npm test && npm run test:integration`.
- `README.md` quick start is verified from a clean checkout.

## Report

### Summary
Branch `task/T90-integration-deploy`, on top of `main` (aa82b2a, nothing to rebase). `scripts/verify.sh --e2e` (full mode plus E2E) is green: 298 unit + 331 integration backend tests including `MigrationsAreInSync`, contract lint and `gen:api` unchanged, both seed validators, `check`, `lint`, 498 Vitest, 173 Playwright integration, 8 E2E against the compose stack (222 s). The compose stack was removed again (`down -v`) and the Docker build cache pruned.
- **Migrations / contract:** the model was already in sync (`MigrationsAreInSync` green after a `--no-incremental` build), so no migration was added. The backend contract test is green and `schema.d.ts` regenerates unchanged; the frontend type-checks.
- **Stubs and harnesses:** `StubEndpoints.cs` deleted. F5↔F6 `CookContext`, F4↔F5 `RecipeEditor` and the F3 import link were already real; the E2E suite exercises all three. F4's import fixture now reads `shared/samples/export-v1.json`.
- **Deploy** (`deploy/`): `compose.yaml` (db `postgres:18` + `pgdata`; api built from `backend/src/RecipesList.Api/Dockerfile`; web built from `deploy/web.Dockerfile`; volumes `images`, `dpkeys`, `caddy_data`, `caddy_config`; only Caddy is published, `${HTTP_PORT:-8080}`), `compose.e2e.yaml` (Testing environment = dev-login, high rate limits), `Caddyfile` (`{$SITE_ADDRESS::8080}`, zstd/gzip, `/api/*` proxy, SPA fallback via `try_files`, immutable caching for `/_app/immutable/*`, `no-cache` for the shell, nosniff, `X-Frame-Options: DENY`, `frame-ancestors 'none'`, Referrer-Policy, Permissions-Policy, COOP, HSTS, no `Server`), `README.md` (first run, Google OAuth, HTTPS, backups/restore, upgrades, E2E), `.env.example` compose section. API image: multi-stage (`sdk:10.0` → `aspnet:10.0`), locked restore, non-root `$APP_UID`, seed files baked in with `Seed__Path=/app/seed`, `Storage__ImagesPath=/data/images`, JSON console logs, `ASPNETCORE_FORWARDEDHEADERS_ENABLED` (client IP for the per-IP auth limit behind Caddy), bash `/dev/tcp` health check on `/api/health`.
- **E2E** (`frontend/tests/e2e`, `playwright.e2e.config.ts`, `npm run test:e2e`): two users through the real UI, serial: A creates a public recipe with a cover photo; B favourites it (seen under Ulubione) and adds a public variant; A sees "cannot edit" and gets the visibility-locked explanation; A's private recipe is edited freely, gets a variant, and the next edit confirms deleting it; B renames the fridge, invites A (link from the clipboard) and stocks milk; A picks B's variant option, scales ×2 with "Mam", deducts from B's fridge, and adds the missing items, which B sees on the shopping list; A exports (download checked) and B imports (footer "oryginalnie: @…"); finally six main screens are scanned for leaked i18n keys/API codes, `undefined`/`NaN`, and common English words. `scripts/verify.sh --e2e` runs it against a fresh `recipes-list-e2e` stack and always tears it down.
- **verify.sh:** new step `node shared/seed/validate.mjs`; `--e2e` = full gate + port check + `compose up --build --wait` + `npm run test:e2e`, with a cleanup hook in the exit trap.
- **Hardening (each with a failing test first):** concurrent same-name variant create/update → 409 `variant.name_taken` (was 500); concurrent invite accept by the same user → 200 for all, one membership (was 500); concurrent undo of one deduction → one 200, the rest 409 `deduction.already_undone`/`deduction.items_changed` (was 500); first login creates the user, fridge and default inside one `TransactionScope` (a failure leaves no user; this also removes the brief `defaultFridgeId: null` for a losing concurrent first login). Tests for the per-user image rate limit added (auth and import were already covered).
- **No email leaks:** every `ApiClient` now goes through `EmailLeakGuard`, a `DelegatingHandler` that fails the request when a JSON response contains an email-like value (`x@y`) anywhere other than the caller's own top-level `email` on `/me*` and the login responses; the whole integration suite (331 tests) passes through it. `ApiClients.Create(factory)` builds all clients; `ApiClient.PatchJsonAsync` replaced B5's local helper.
- **No English in the UI:** `src/lib/i18n/no-english-ui.test.ts` fails on any literal word in component markup (text nodes, `aria-label`/`title`/`placeholder`/`alt`/`label`) outside `{…}` expressions; the codebase is clean. The E2E screen scan covers the runtime side.
- **Query review** (`optimizing-ef-core-queries`, source-only): lists and matrix already have statement-count tests; no N+1 found. Read paths project or use `AsNoTracking`; the search uses the trigram-indexed normalized columns. Only bounded per-row loops remain (import resolves each tag/utensil with one call, at most a few per file); no change needed. Not measured against real data volumes.
- **Integration defects fixed:** the recipe view did not show the cover photo at all (found by E2E; now shown with alt "Zdjęcie przepisu", integration test added); `+error.svelte` was the only screen without `<Page>` (fixed, integration test added). All other routes start with `<Page>` (`/lodowka/[id]` through `FridgeScreen`).
- **README quick start verified** from a clean export of the branch (`git archive`): `docker compose -f deploy/compose.yaml up -d --build` served the app on :8080 in Production (`devLoginEnabled: false`, SPA fallback, zstd, migrations and seed applied); torn down afterwards.

### Deviations
- **CSP lives in `svelte.config.js` (`kit.csp`, hash mode), not in the Caddyfile.** The SPA shell has an inline bootstrap script, so a header policy would need `unsafe-inline`; SvelteKit writes a `<meta>` CSP with the script's hash. Caddy adds `frame-ancestors 'none'` (a `<meta>` cannot). Allowed: self, Google Identity Services (`accounts.google.com/gsi/*`), `data:`/`blob:`/`*.googleusercontent.com` images, inline styles. Untested against the real Google script (no client ID here).
- **The dev-only harness `/lodowka/harness-odejmowanie` stays.** Its four integration tests check the dialog's edge cases (warnings, switched-off rows, 409 on undo, add-missing amounts) with tiny fixed fixtures; moving them onto the recipe view would need a full matrix fixture for little gain. It is gated like `/ui` and is not in production builds; the real dialog on the recipe view is covered by the E2E suite. Its comment was updated.
- **`--e2e` runs the full gate first**, as the T00 header of `verify.sh` described, so one `--e2e` run covers the full gate too.
- **Web container has no health check** (Caddy listens on `SITE_ADDRESS`, which can be a domain); compose waits for it to run, after the API is healthy.
- **Production cookie on http://localhost:** `__Host-rl_session` is `Secure`; browsers accept it on `localhost` only. Any other host needs HTTPS (documented).
- Data-protection keys persist in the `dpkeys` volume (no code change; the default key directory is mounted) so upgrades don't sign everyone out.
- `GSS Encryption Mode=Disable` in the compose connection string silences Npgsql's attempt to load Kerberos in the runtime image.
- Files outside the owned list: root `.dockerignore`; `frontend/svelte.config.js` (CSP); `frontend/package.json` (`test:e2e`); `scripts/verify.sh`; `frontend/src/routes/przepisy/[id]/+page.svelte` + `i18n/pl/variants.ts` (`coverAlt`, additive); `frontend/src/routes/+error.svelte`; `frontend/src/routes/lodowka/harness-odejmowanie/+page.ts` (comment); `tests/integration/{smoke,recipe-view}.spec.ts`, `mocks/recipes.ts`; backend `VariantService`, `FridgeService`, `DeductionService`, `UserAccounts` (race fixes) and the test harness files.

### Model changes
None. No migration was needed.

### Contract changes
None. (The new 409s use codes and statuses those endpoints already document; invite accept stays 200.)

### Dependencies added
None. (Docker base images: `mcr.microsoft.com/dotnet/sdk:10.0`, `mcr.microsoft.com/dotnet/aspnet:10.0`, `node:24-alpine`, `caddy:2-alpine`, `postgres:18`.)

### Requests for other tasks
None.

### Follow-ups
- **Import race (B6):** a concurrent create of the same new product/tag/utensil inside the import transaction still fails with 500 (the aborted Postgres transaction prevents a simple retry; it needs a savepoint or a pre-insert `ON CONFLICT`). Not fixed: larger than the ~50-line budget.
- **Export with a missing image file (B6)** still answers 500.
- **Shopping PATCH cannot clear an amount** (null = unchanged). Left as is: an explicit clear is a contract change (API.md + openapi.yaml + `schema.d.ts` + F6 UI) with no functional decision behind it.
- Deductions are never pruned (B5); selection-memory preference recompute reads all of a user's selections (B4).
- Real Google sign-in and the CSP with the GIS script are untested without a client ID.
- A fixed web health check could be added if `SITE_ADDRESS` stays `:8080` (e.g. busybox `wget` on `/`).
- Coordinator: set T90 on the board; E2E runs need port 8080 free (or `E2E_HTTP_PORT`).
