# T00: Foundation

**Wave 0 · runs alone · depends on: Phase 0 environment**

## Goal
Create the skeleton that every other task builds on. Afterwards both apps build and run (empty), all hotspot files exist, the full data model and initial migration exist, the contract is in OpenAPI, and the test harnesses work.

## Read first
`CLAUDE.md`, `PROJECT.md`, `TECHNICAL.md`, `docs/API.md`, `docs/IMPLEMENTATION_PLAN.md` (all of it, especially §4 and §5).

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `svelte-code-writer`, `svelte-core-bestpractices`.

## Deliverables

### Repository
- `git init` (if not already), `.gitignore` (dotnet, node, `.env`, `build/`, `bin/`, `obj/`, test results), `.editorconfig`, `.nvmrc` (24), and a root `README.md` with quick-start commands.
- Repository layout exactly as in TECHNICAL.md §2.

### Contract
- `docs/api/openapi.yaml`: an OpenAPI 3.1 transcription of **all** of API.md: every path, method, request/response schema, status code and the ProblemDetails schema with `code`. Validate it with `npx @redocly/cli lint` (or an equivalent).

### Backend
- `RecipesList.slnx` with 3 src projects and 2 test projects. `Directory.Build.props` (nullable, warnings as errors, analyzers), `Directory.Packages.props`, `global.json`.
- **All entities** in `Core` (one folder per feature) with EF configurations in `Infrastructure`, matching this model:
  - `User`(Id, GoogleSubject unique, Email, Nickname?, NormalizedNickname? unique, AvatarUrl?, IsAdmin, LiquidUnits, DefaultFridgeId?, CreatedAt)
  - `Product`(Id, Name, NormalizedName unique+trgm, Category, MeasureKind, DisplayClass, HasSizes, AllowHalf, GramsPerMl?, GramsPerPiece?, DefaultPackageAmount?, PieceForms jsonb?, CreatedById?, CreatedAt)
  - `Tag`(Id, Name, NormalizedName unique, IsSystem) · `Utensil`(Id, Name, NormalizedName unique)
  - `Image`(Id, OwnerId, Width, Height, SizeBytes, CreatedAt)
  - `Recipe`(Id, AuthorId, Title, NormalizedTitle trgm, Description?, CoverImageId?, PrepMinutes?, CookMinutes?, Visibility, IsArchived, CookCount, CreatedAt, UpdatedAt, Version=xmin) plus `RecipeTag`, `RecipeUtensil` join tables
  - `RecipeIngredient`(Id, RecipeId, Position, ProductId, QuantityValue?, QuantityUnit?, QuantitySize?, BaseAmount?, Note?, Optional)
  - `RecipeStep`(Id, RecipeId, Section, Position, Blocks jsonb)
  - `Variant`(Id, RecipeId, AuthorId, Name, Description?, Visibility, IngredientOps jsonb, StepOps jsonb, UtensilIdsAdded uuid[], UtensilIdsRemoved uuid[], PrepMinutes?, CookMinutes?, CookCount, CreatedAt, UpdatedAt, Version)
  - `RecipeFavorite`(UserId+RecipeId PK, CreatedAt) · `VariantFavorite`(UserId+VariantId PK, CreatedAt), both cascade-deleted with their recipe or variant
  - `RecipeSelection`(UserId+RecipeId PK, Picks jsonb, Scaling jsonb?, UpdatedAt) · `ProductPreference`(UserId+FromProductId+ToProductId PK, Weight)
  - `Fridge`(Id, Name, OwnerId, CreatedAt) · `FridgeMember`(FridgeId+UserId PK, Role, JoinedAt) · `FridgeInvite`(Id, FridgeId, Token unique, CreatedById, ExpiresAt, RevokedAt?, CreatedAt)
  - `FridgeItem`(Id, FridgeId, ProductId, PackageAmount numeric, RemainingPercent numeric(5,2), Note?, AddedById, CreatedAt, UpdatedAt, Version)
  - `Deduction`(Id, FridgeId, UserId, RecipeId?, VariantId?, CreatedAt, UndoneAt?, Lines jsonb including a full snapshot of each touched item)
  - `ShoppingListItem`(Id, FridgeId, ProductId?, Text?, Amount?, Checked, AddedById, CreatedAt, Version)
  - Postgres extensions `pg_trgm` and `unaccent`. snake_case naming. Initial migration `0001_Initial`.
- Core shared code: `ICurrentUser`, `IAppDbContext`, domain exceptions, **`NameNormalizer` (implemented)**, **`QuantityConverter : IQuantityConverter` (implemented, PROJECT §5.2)**, **`VisibilityRules` (implemented, PROJECT §4.4 and §6.1)**: `IQueryable<Recipe> VisibleRecipes(Guid userId)` and `IQueryable<Variant> VisibleVariants(Guid userId)`. Each translates to a single SQL query (`fridges` = an `EXISTS` over two `fridge_members` rows that share a fridge), with a supporting index on `fridge_members(user_id, fridge_id)`, and is covered by integration tests for all 3 × 3 recipe/variant visibility combinations, and the interfaces `IImageStorage` and `IVariantEngine` (signatures from the plan §5.1, bodies `throw new NotImplementedException()`).
- Api: `Program.cs` with ProblemDetails + an `IExceptionHandler` mapping domain exceptions, JSON options (TECHNICAL §3.3), the CSRF header middleware, cookie auth, an onboarding-gate filter, `AddOpenApi`, health endpoints, the rate-limiter policies, **dev-login endpoint (working)**, `ICurrentUser` from claims, and `Database:MigrateOnStartup`.
- One feature registration file per feature (`AuthFeature.cs`, `CatalogFeature.cs`, `ImagesFeature.cs`, `RecipesFeature.cs`, `VariantsFeature.cs`, `FridgesFeature.cs`, `ShoppingListsFeature.cs`, `TransferFeature.cs`, `FavoritesFeature.cs`), each with `Add<Feature>(IServiceCollection)` and `Map<Feature>(IEndpointRouteBuilder)`. Endpoint bodies return `TypedResults.StatusCode(501)` until the feature task implements them. `Program.cs` calls all of them.
- Tests:
  - UnitTests: `NameNormalizer` tests; `QuantityConverter` driven by every file in `shared/test-vectors/quantities/`.
  - IntegrationTests: an `ApiFactory` (Testcontainers Postgres 18, `EnsureCreated`, Respawn), an `ApiClient` helper with `LoginAsync(nickname)` via dev-login that sets the CSRF header, a `ContractAssert` helper that validates a JSON body against a schema in `openapi.yaml` by name (JsonSchema.Net), and tests for health, dev-login, the 401 when not logged in, the 400 without the CSRF header, and `MigrationsAreInSync`.

### Shared
- Test-vector directories with one file per task (TECHNICAL §6), a JSON schema per kind, and **directory loaders** on both sides (C# and TS) that read every `*.json` file and fail on duplicate case ids:
- `shared/test-vectors/quantities/T00.json`: ≥ 25 cases (every unit; mass↔volume via density; count↔mass; sizes; incompatible ⇒ null).
- `shared/test-vectors/formatting/T00.json`: ≥ 30 cases covering each rule in PROJECT §5.4, including the examples given there.
- `shared/test-vectors/matching/`: empty directory plus its schema (F2 fills it).
- `shared/test-vectors/variants/T00.json`: 5 hand-written cases (modify amount; swap product; remove; add after; step replace plus a utensil change), each with `base`, `variants`, `expectedResolved` and `expectedMatrix`.
- `shared/seed/`: empty-but-valid `products.json`, `tags.json` (system tag `szybkie` only) and `utensils.json`, each with its JSON schema (`*.schema.json`). S1 fills them.

### Frontend
- SvelteKit 2 + Svelte 5 + TS strict, `adapter-static` SPA, Tailwind 4, bits-ui, ESLint/Prettier/svelte-check, Vitest (jsdom) and Playwright configured (projects: `mobile` = Pixel 7, `desktop`).
- **Ports from the environment** (plan §4.3): Vite `server.port = WEB_PORT`, `preview.port = PREVIEW_PORT`, the proxy `/api` → `http://localhost:${API_PORT}`, the Playwright `webServer` and `baseURL` use `PREVIEW_PORT`, and the API's Kestrel URL comes from `API_PORT` (in `launchSettings.json` and `Program.cs`). `.env.local` is git-ignored and loaded by a tiny helper on both sides.
- **Every dependency from plan §4.5** installed up front, with versions pinned in the lockfiles.
- `npm run gen:api` (openapi-typescript → `src/lib/api/schema.d.ts`) and `src/lib/api/client.ts` (TECHNICAL §4.2), with unit tests for error mapping.
- `src/lib/i18n/pl/` with all feature files, plus `errors.ts` containing **every** code from API.md §9 and a test that enforces this.
- Every route from TECHNICAL §4.2 as a placeholder page, a root layout with a simple nav, and an auth guard in `+layout.ts` (`/me` → redirect to `/logowanie` or `/witaj`).
- `src/lib/domain/*.ts` with the exact signatures from the plan §5.2, throwing `not implemented`. `src/lib/domain/types.ts` re-exports the DTO types from the schema.
- `tests/integration/mocks/` with a helper `mockApi(page, handlers)` and per-feature empty fixture files. One smoke test: the app loads `/przepisy` with a mocked `/me`.
- An ESLint rule or Vitest test that fails on Polish string literals outside `src/lib/i18n/pl/`.

### Deploy and tooling
- `deploy/compose.dev.yaml` (postgres:18 on 5432 with a volume) and `deploy/.env.example`.
- **`scripts/verify.sh`** exactly as specified in plan §4.6 (`--agent`, default full mode, `--e2e` placeholder), using `set -euo pipefail` and printing a clear step name before each step. It must work from any worktree, using paths relative to the script.
- `docs/COORDINATOR.md` status board: set T00 to `merged` once accepted.

## Acceptance
- `docker compose -f deploy/compose.dev.yaml up -d`, then `dotnet run` (the API starts and migrates); `dotnet test` is green.
- `npm ci && npm run check && npm run lint && npm test && npm run test:integration` are green in `frontend/`.
- `openapi.yaml` lints cleanly, and `schema.d.ts` is generated and committed.
- In Development with no Google client ID, `/logowanie` shows a dev-login form that works end to end against the running API.
- `scripts/verify.sh` (full mode) is green on `main`. Running two copies of the stack with different `API_PORT`/`WEB_PORT`/`PREVIEW_PORT` values at the same time works.

## Out of scope
Feature logic beyond dev-login, `QuantityConverter` and `NameNormalizer`; visual design (F0).

## Progress notes (work in progress, NOT the final Report)

Paused on user request on branch `task/T00-foundation` (worktree `.claude/worktrees/agent-a21934c230a63d13f`). All work is committed; the tree is clean.

### Done and green
- Repo basics (`.gitignore`, `.editorconfig`, `.nvmrc`), `deploy/compose.dev.yaml` (host port via `POSTGRES_PORT`, default 5432 — a system PostgreSQL 14 already listens on 5432 on this machine, so local runs used `POSTGRES_PORT=5433`), `deploy/.env.example`.
- Backend: solution, all entities, configurations, migration `0001_Initial` (applies cleanly), `NameNormalizer`, `QuantityConverter`, `VisibilityRules`, domain exceptions, `IImageStorage`/`IVariantEngine` + NotImplemented placeholders, Program.cs (ProblemDetails + exception handler, CSRF middleware, cookie auth, onboarding gate, rate limits, health, OpenAPI, `Database:MigrateOnStartup`, `API_PORT`/.env.local), one `*Feature.cs` per feature with 501 stubs; working dev-login, `/auth/config`, `/auth/logout`, minimal `GET /me`.
- Backend tests: `dotnet test` (Microsoft.Testing.Platform, set in `backend/global.json`) → 97 passed; `dotnet test --filter-not-trait "Category=Migrations"` → 95. `dotnet format --verify-no-changes` and `dotnet build` (0 warnings) clean.
- Contract: `docs/api/openapi.yaml` + `docs/api/redocly.yaml`; `npm run lint:api` clean; `schema.d.ts` generated and committed.
- Shared: vectors (quantities 34, formatting 66, variants 5, empty matching), schemas in `shared/test-vectors/schemas/`, seed files + schemas; `npm run validate:shared` OK.
- Frontend: SvelteKit 2.70 / Svelte 5 / TS 5.9 / Vite 8, all §4.5 deps pinned; `npm run check`, `npm run lint`, `npm test` (28), `npm run test:integration` (6, mobile + desktop) green; `node scripts/svelte-autofix.mjs` clean on every .svelte file.

### Remaining
1. `scripts/verify.sh` (plan §4.6): `--agent` / full / `--e2e` placeholder, `set -euo pipefail`, step names, paths relative to the script; steps: dotnet format verify, dotnet build, dotnet test (agent: `--filter-not-trait "Category=Migrations"`), `npm run lint:api` + `npm run gen:api` + `git diff --exit-code frontend/src/lib/api/schema.d.ts`, `npm run check/lint/test`, `npm run test:integration`, `npm run validate:shared`. Run `npm ci` only when node_modules is missing/stale. Must not depend on the machine-specific cache variables.
2. Root `README.md` (quick start; optional `NUGET_PACKAGES` / `PLAYWRIGHT_BROWSERS_PATH` / `npm_config_cache` exports; `POSTGRES_PORT` note; `npx playwright install chromium`).
3. Manual acceptance checks: dev-login end to end through the Vite dev server against a running API; two stacks with different ports at the same time.
4. `git rebase main`, full `scripts/verify.sh` green, then append the final `## Report` (Summary, Deviations, Model changes, Contract changes, Dependencies added, Requests for other tasks, Follow-ups) and commit. Do not edit `docs/COORDINATOR.md`.

### Points for the Report (decided so far)
- Deviations: Core references the base `Microsoft.EntityFrameworkCore` package (plan §5.1 `IAppDbContext` exposes `DbSet<T>`; TECHNICAL §3.1 says no EF in Core). C# enum is `VisibilityLevel` (namespace clash with `Core.Visibility`). Archived recipes in `VisibleRecipes`: author, or a variant author who could otherwise see it. Stored variant ops carry a server-computed `baseAmount`. Dev cookie is `rl_session` (no `__Host-` prefix over http in Development). `dotnet test` uses Microsoft.Testing.Platform (xunit.v3 4.x requires it); `xunit.runner.visualstudio`/`Microsoft.NET.Test.Sdk` not used. SvelteKit 2 / TypeScript 5.9 pinned instead of the newer Kit 3 / TS 7 (spec says Kit 2; tooling compatibility). Formatting vectors: `text` excludes `~`. Quantity converter: size ignored when the product has no sizes; base amounts rounded to 4 decimals. `/auth/config` and `/auth/logout` also use the `auth` rate limit. T00 implemented minimal `GET /me`/logout/config (B1 owns and may replace).
- Contract changes: new codes `auth.required`, `request.not_found`, `request.forbidden`, `request.invalid` (+ client-only `network.error` in errors.ts); dev-login errors 400/409; option-key amount format clarified (invariant, no trailing zeros).
