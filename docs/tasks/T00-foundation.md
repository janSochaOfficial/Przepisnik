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

## Report

### Summary
Everything listed under Deliverables is done, on branch `task/T00-foundation`, rebased onto `main`.
- **Repository:** `.gitignore`, `.editorconfig`, `.nvmrc` (24), and a root `README.md` (quick start, ports, the quality gate, optional cache variables). The layout follows TECHNICAL §2.
- **Contract:** `docs/api/openapi.yaml` (OpenAPI 3.1, every path in API.md) plus `docs/api/redocly.yaml`. `npm run lint:api` is clean, and `frontend/src/lib/api/schema.d.ts` is generated and committed.
- **Backend:** `RecipesList.slnx` (3 src + 2 test projects), central package management, and warnings as errors. It has every entity with one EF configuration per entity, snake_case naming, `pg_trgm` + `unaccent`, and migration `0001_Initial` (it applies cleanly and `MigrationsAreInSync` is green). Implemented: `NameNormalizer`, `QuantityConverter`, `VisibilityRules` (single-query visibility with an `EXISTS` over `fridge_members`, index `fridge_members(user_id, fridge_id)`, and the 3 × 3 matrix tests). `IImageStorage` and `IVariantEngine` have NotImplemented placeholders. `Program.cs` has ProblemDetails with the exception handler, the CSRF middleware, cookie auth, the onboarding gate, rate limits, health/ready, OpenAPI, `Database:MigrateOnStartup`, and `API_PORT` / `.env.local`. There is one `*Feature.cs` per feature with 501 stubs, plus a working dev-login, `/auth/config`, `/auth/logout` and a minimal `GET /me`.
- **Backend tests:** 58 unit tests and 39 integration tests (Testcontainers Postgres 18, `EnsureCreated` + Respawn, `ApiClient.LoginAsync`, `ContractAssert` against `openapi.yaml`).
- **Shared:** test vectors (quantities 34, formatting 66, variants 5, and an empty `matching/`) with one schema per kind, and directory loaders in C# and TS that fail on duplicate ids. The seed files (`szybkie` tag only) have schemas. `npm run validate:shared` checks all of them.
- **Frontend:** SvelteKit 2 + Svelte 5 + TS strict, adapter-static SPA, Tailwind 4, and every dependency from §4.5, pinned. It has the typed client with error mapping, `i18n/pl/` (with a test that `errors.ts` covers every API.md §9 code) and a guard against Polish literals outside `i18n/pl/`. Every route is a placeholder; the root layout has nav and an auth guard; the domain stubs match plan §5.2. Also in place: `mockApi` + per-feature fixtures, and the smoke test (mobile + desktop). Ports come from the environment or `.env.local`.
- **Tooling:** `deploy/compose.dev.yaml` (postgres:18) and `deploy/.env.example`. `scripts/verify.sh` follows plan §4.6: `--agent`, full by default, and an `--e2e` placeholder. It uses `set -euo pipefail`, prints a named step before each step, names the failing step, uses paths relative to the script, and runs `npm ci` only when `node_modules` is missing or older than the lockfile.
- **Acceptance, verified after rebasing onto `main`:**
  - Full `scripts/verify.sh` is green, run from outside the repo (all 97 backend tests, 28 Vitest, 6 Playwright), including the `npm ci` path.
  - Manual check: two stacks ran at the same time, API 5100 + web 5200 and API 5190 + web 5290, each with its own database (`recipes_t00`, `recipes_t00b`). Both migrated on startup.
  - Through each Vite proxy: `/api/v1/auth/config` answered `devLoginEnabled: true`, `/me` answered 401 `auth.required`, and dev-login without the header answered 400 `request.missing_csrf_header`. Dev-login with the header answered 200 and `/me` then answered 200.
  - In a real browser (Playwright), `/przepisy` redirected to `/logowanie`, the dev-login form logged in and landed back on `/przepisy`, and `/me` returned the new nickname, on both stacks. The only console error was the expected 401 before login.
  - All servers were stopped afterwards and `recipes_t00b` was dropped. The dev Postgres container was already running and was left running.

### Deviations
- **Accepted by coordinator:** Core references the base `Microsoft.EntityFrameworkCore` package (no provider and no Design package). The fixed `IAppDbContext` (plan §5.1) exposes `DbSet<T>`, while TECHNICAL §3.1 says there is no EF in Core.
- **Accepted by coordinator:** the error codes `auth.required`, `request.not_found`, `request.forbidden` and `request.invalid` are added. They are in API.md §9 and are now also named in `openapi.yaml` (the `ProblemDetails.code` description and the shared 400/403/404 responses). `network.error` is client-only (`errors.ts`) and is not in the contract.
- **Accepted by coordinator:** `deploy/compose.dev.yaml` takes the host port from `POSTGRES_PORT` (default 5432). This is documented in the README and in `deploy/.env.example`.
- **Test runner is VSTest, not Microsoft.Testing.Platform.** The coordinator accepted MTP on the condition that plain `dotnet test backend` works from the repo root. That cannot hold with MTP on the .NET 10 SDK. The MTP mode of `dotnet test` rejects a positional directory ("should be via '--project' or '--solution'"), and it is opted into through the nearest `global.json`, which a run from the root does not find. I took the conservative option that keeps the documented commands working: the test projects use `xunit.runner.visualstudio` + `Microsoft.NET.Test.Sdk` with `IsTestingPlatformApplication=false`, and the `test.runner` entry is removed from `backend/global.json`. The commands `dotnet test backend` (root), `cd backend && dotnet test`, and `dotnet test --filter "Category!=Migrations"` (agent mode) all work. The MTP flag `--filter-not-trait` therefore does not exist. Agents use the VSTest filter, as `scripts/verify.sh --agent` does.
- **`scripts/verify.sh --e2e`** stops right away with "added by T90" instead of running the full gate first. The E2E suite does not exist yet.
- The C# enum is named `VisibilityLevel`, because `Visibility` clashes with the `Core.Visibility` namespace.
- **Archived recipes in `VisibleRecipes`:** visible to the author, and to a variant author who could otherwise see the recipe.
- Stored variant ops carry a server-computed `baseAmount`.
- The dev session cookie is `rl_session`. The `__Host-` prefix cannot be used over http in Development.
- SvelteKit 2 and TypeScript 5.9 are pinned instead of the newer Kit 3 / TS 7: the spec says Kit 2, and the tooling must be compatible.
- **Formatting vectors:** `text` excludes `~`.
- **Quantity converter:** the size is ignored when the product has no sizes, and base amounts are rounded to 4 decimals.
- `/auth/config` and `/auth/logout` also use the `auth` rate-limit policy.
- T00 implemented minimal `GET /me`, `/auth/logout` and `/auth/config`. B1 owns them and may replace them.

### Model changes
None beyond the initial model. `0001_Initial` contains the full model from this task file.

### Contract changes
- The generic error codes listed above are in both API.md §9 and `openapi.yaml`.
- `POST /auth/dev-login` errors: 400 `nickname.invalid`, 409 `nickname.taken`.
- The option-key amount format is clarified: invariant culture, with no trailing zeros.

### Dependencies added
Everything in plan §4.5, installed up front. In addition, beyond the list: `Microsoft.NET.Test.Sdk` 18.11.0 and `xunit.runner.visualstudio` 4.0.1 (the VSTest runner, see Deviations), and the base `Microsoft.EntityFrameworkCore` in Core.

### Requests for other tasks
- **B1:** may replace the minimal `GET /me`, `/auth/logout` and `/auth/config` implemented here, keeping the contract.
- **T90:** wire the `--e2e` mode of `scripts/verify.sh` to the compose stack and `tests/e2e`.

### Follow-ups
- **Coordinator:** set T00 to `merged` in `docs/COORDINATOR.md` after merging. This task did not edit that file.
- Two `dotnet run` processes from the **same** checkout race on the build output (static web assets cache). For a second stack in one checkout, use `dotnet run --no-build`. Separate worktrees are not affected.
