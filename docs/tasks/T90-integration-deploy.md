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
