# Technical standards

This document is binding for all code in the repository, whether a person or an agent writes it. If a vendored skill in `.claude/skills/` contradicts it, this document wins.
Functional behaviour is defined in [PROJECT.md](PROJECT.md), and the HTTP contract in [docs/API.md](docs/API.md).

## 1. Stack and versions

| Area | Choice | Version policy |
|---|---|---|
| Backend runtime | .NET **10** (LTS), C# 14 | pinned via `global.json` (`rollForward: latestFeature`) |
| Web framework | ASP.NET Core **Minimal APIs** | — |
| ORM | EF Core 10 + `Npgsql.EntityFrameworkCore.PostgreSQL` | — |
| Database | PostgreSQL **18** (extensions: `pg_trgm`, `unaccent`) | Docker image `postgres:18` |
| Auth | Google ID token (`Google.Apis.Auth`) → ASP.NET Core cookie auth | — |
| Images | `SkiaSharp` (+ `SkiaSharp.NativeAssets.Linux.NoDependencies`) | MIT licensed |
| Backend tests | xUnit v3, Shouldly, NSubstitute, Testcontainers.PostgreSql, Respawn, `Microsoft.AspNetCore.Mvc.Testing`, JsonSchema.Net | — |
| Frontend runtime | Node **24 LTS**, npm | pinned via `.nvmrc` + `engines` |
| Frontend | SvelteKit 2, **Svelte 5 (runes only)**, TypeScript (strict), Vite | — |
| Styling | Tailwind CSS 4 + design tokens as CSS custom properties | — |
| Headless UI | `bits-ui` (dialogs, bottom sheets, switches, comboboxes) | — |
| API client | `openapi-typescript` (types generated from the contract) + `openapi-fetch` | — |
| Frontend tests | Vitest, `@testing-library/svelte`, jsdom; Playwright (`@playwright/test`) | — |
| Lint/format | ESLint (flat config) + `eslint-plugin-svelte`, Prettier + `prettier-plugin-svelte`, `svelte-check`; `dotnet format` + analyzers | — |
| Reverse proxy / static hosting (prod) | Caddy 2 | — |

Lock files (`package-lock.json`, `packages.lock.json` with `RestorePackagesWithLockFile`) are committed. NuGet versions are managed centrally in `Directory.Packages.props`.

## 2. Repository layout

```
/
├── PROJECT.md  TECHNICAL.md  CLAUDE.md
├── scripts/verify.sh               # the single quality gate (plan §4.6)
├── docs/
│   ├── API.md                      # human-readable contract
│   ├── api/openapi.yaml            # normative machine contract (generated from API.md in T00, then kept in sync)
│   ├── IMPLEMENTATION_PLAN.md  COORDINATOR.md
│   ├── AGENT_DECISIONS.md          # decisions agents made, for review
│   └── tasks/                      # one file per agent task (only while a wave is in progress)
├── shared/
│   ├── test-vectors/<kind>/<TaskID>.json   # fixtures used by BOTH backend and frontend tests; one file per task
│   ├── samples/                    # sample files (e.g. export-v1.json)
│   └── seed/                       # products.json, tags.json, utensils.json (Polish seed data)
├── backend/
│   ├── RecipesList.slnx
│   ├── Directory.Build.props  Directory.Packages.props  global.json  .editorconfig
│   ├── src/
│   │   ├── RecipesList.Api/            # composition root, endpoints, DTOs, auth, middleware
│   │   ├── RecipesList.Core/           # entities, domain logic, service interfaces + implementations
│   │   └── RecipesList.Infrastructure/ # DbContext, EF configurations, migrations, storage, Google validator
│   ├── tests/
│   │   ├── RecipesList.UnitTests/
│   │   └── RecipesList.IntegrationTests/
│   └── http/                       # .http files per feature
├── frontend/
│   ├── src/
│   │   ├── lib/
│   │   │   ├── api/                # generated schema.d.ts + typed client wrapper
│   │   │   ├── domain/             # pure TS: quantities, plurals, scaling, variant matching
│   │   │   ├── state/              # runes-based classes (session, selection memory, …)
│   │   │   ├── components/         # reusable UI (ui/ = primitives, feature folders)
│   │   │   └── i18n/pl/            # ALL user-facing strings, one file per feature + index.ts
│   │   └── routes/
│   ├── tests/
│   │   ├── integration/            # Playwright with mocked API
│   │   └── e2e/                    # Playwright full stack (docker compose)
│   └── (configs)
└── deploy/
    ├── compose.yaml                # full stack: db, api, web (Caddy)
    ├── compose.dev.yaml            # db only, for local development
    ├── Caddyfile
    └── .env.example
```

## 3. Backend architecture

### 3.1 Projects and dependency rules
- `Core` holds entities, enums, domain services and pure logic (variant diff/resolve/matrix, deduction allocation, quantity conversion, name normalization). It **must not reference** ASP.NET Core, EF Core or Npgsql. Data access goes through the `IAppDbContext` abstraction described in 3.4.
- `Infrastructure` → `Core`. It holds `AppDbContext`, entity configurations, migrations, `IImageStorage` (local disk), `IGoogleTokenValidator` and seeders.
- `Api` → `Core` and `Infrastructure`. It holds endpoints, request/response DTOs, mapping, auth, middleware and DI wiring.

### 3.2 Feature organisation
Code is grouped by feature, not by technical layer, inside each project:
`Auth`, `Users`, `Catalog` (products, tags, utensils), `Images`, `Recipes`, `Variants`, `Favorites`, `Fridges`, `Deductions`, `ShoppingLists`, `Transfer` (import/export).
Visibility (PROJECT §4.4) is enforced in **one place**, `Core/Visibility/VisibilityRules`, which every recipe and variant read composes with. Never re-implement the fridge-membership check inline.

```
RecipesList.Api/Features/Recipes/RecipeEndpoints.cs     # static MapRecipes(this IEndpointRouteBuilder)
RecipesList.Api/Features/Recipes/RecipeDtos.cs          # sealed records
RecipesList.Core/Recipes/RecipeService.cs (+ IRecipeService)
RecipesList.Core/Recipes/Recipe.cs                      # entity
RecipesList.Infrastructure/Persistence/Configurations/RecipeConfiguration.cs
```

### 3.3 Endpoint conventions (aligned with the `dotnet-webapi` skill)
- Minimal APIs only, with one `Map<Feature>` extension per feature, all under `app.MapGroup("/api/v1").RequireAuthorization()`.
- Handlers are thin: bind → call service → map → `TypedResults`, using an explicit `Results<…>` return type.
- Every handler takes a `CancellationToken` and passes it all the way down.
- DTOs are `sealed record`s with `<summary>` XML docs. Entities are **never** serialized.
- Metadata on every endpoint: `.WithName()`, `.WithSummary()`, `.Produces…()`. OpenAPI is generated with the built-in `AddOpenApi()`; **no Swashbuckle**.
- Validation uses `builder.Services.AddValidation()` with data annotations for shape. Business rules are validated in services and raise domain exceptions (see 3.6).
- JSON: camelCase, enums as camelCase strings (`JsonStringEnumConverter(JsonNamingPolicy.CamelCase)`), strict number handling, no case-insensitive matching, duplicate properties rejected, nulls written explicitly (the contract treats `null` and absent the same in responses; clients send explicit `null`).
- Date/time: `DateTimeOffset` in DTOs (UTC, ISO 8601). `TimeProvider` is injected wherever "now" is used.
- POST that creates → `201 Created` with `Location`. DELETE → `204`. Full update → `PUT` returning `200` with the resource.

### 3.4 Persistence
- `AppDbContext` implements `IAppDbContext` (it exposes `DbSet`s via `IQueryable`-friendly properties and `SaveChangesAsync`). Services use EF directly through this interface, with **no repository layer**.
- IDs: `Guid` generated with `Guid.CreateVersion7()` in the entity constructor.
- Table and column names are snake_case (`EFCore.NamingConventions`).
- Optimistic concurrency uses PostgreSQL `xmin` mapped as `uint Version` (`.IsRowVersion()`) on Recipe, Variant, FridgeItem and ShoppingListItem. The API sends it as `version`, and a mismatch → `409 concurrency.conflict`.
- Recipe body (ingredients, steps, blocks) is stored relationally: `recipe_ingredients`, `recipe_steps` (with `section` enum), and step blocks as a **jsonb** column on `recipe_steps`. Variant operations are stored as a **jsonb** array on `variants`. Selection memory is jsonb.
- Normalized names (`normalized_name`, with diacritics removed and lower-cased in C# via `NameNormalizer`) carry unique indexes. Search uses `pg_trgm` GIN indexes on `normalized_name` and `recipes.normalized_title`.
- **Migrations**: only the owner of the data model (task T00, later T90 or a coordinating agent) adds migrations. Parallel tasks must not create migrations; if a task needs a schema change, it reports it (see the plan). Migrations are applied at startup when `Database__MigrateOnStartup=true` (the default in Development and Docker). Integration tests build the schema with `EnsureCreated()`, and the separate `MigrationsAreInSync` test (trait `Category=Migrations`) checks that there are no pending model changes.
- Seed data is loaded from `shared/seed/*.json` by an idempotent seeder at startup (upsert by normalized name).
- Read queries project to DTOs or use `AsNoTracking()`. Lists are paged. Follow the `optimizing-ef-core-queries` skill when reviewing queries.

### 3.5 Authentication and authorization
- Flow: the frontend obtains a Google **ID token** with Google Identity Services and calls `POST /api/v1/auth/google`. The API validates it with `GoogleJsonWebSignature.ValidateAsync`, checking the audience against `Auth__Google__ClientId`. It then upserts the user by Google `sub` and signs in with **cookie authentication**.
- Cookie: `__Host-rl_session`, `HttpOnly`, `Secure` (relaxed in Development over http), `SameSite=Lax`, 30-day sliding expiry. The API returns 401 or 403 and **never redirects**.
- CSRF: same-origin deployment plus `SameSite=Lax`, and every mutating request must carry the header `X-Requested-With: fetch`. A middleware rejects mutating requests without it with 400.
- `POST /api/v1/auth/dev-login` exists **only** when `ASPNETCORE_ENVIRONMENT` is `Development` or `Testing`. Integration and E2E tests use it.
- Authorization is enforced **in services**, not only at endpoints. Every service method takes the current user ID through `ICurrentUser`. Missing permission → `403` (or `404` when the resource's existence must not leak, e.g. an invite or fridge the user is not a member of).
- Rate limiting (built-in limiter): `auth/*` 10/min per IP; `images` 30/min per user; `recipes/import` 10/min per user.

### 3.6 Errors
- Every error is an RFC 9457 **ProblemDetails** response with the extension `code` (stable, dotted, English; e.g. `recipe.not_found`, `nickname.taken`, `quantity.unit_incompatible`). Validation errors add `errors: { "<camelCase.path>": ["<code>"] }`.
- Domain exceptions (`NotFoundException`, `ForbiddenException`, `ConflictException`, `DomainValidationException`), each carrying a code, are mapped by an `IExceptionHandler` in `Api/Middleware/`.
- Messages are English and meant for developers. **The frontend maps `code` to Polish text** in `pl.ts`. The full code list is in API.md §9.

### 3.7 Images
- `IImageStorage` with a `LocalDiskImageStorage` implementation at `Storage__ImagesPath` (Docker volume). Files are `{id}.webp` and `{id}_thumb.webp`.
- On upload: validate magic bytes (JPEG, PNG or WebP only), size ≤ 10 MB, decode with SkiaSharp, apply EXIF orientation, resize to ≤ 1600 px and 400 px, encode as WebP at quality 82. Metadata is dropped by re-encoding.
- `GET /api/v1/images/{id}` is served with `Cache-Control: public, max-age=31536000, immutable`. Images require authentication (the cookie is sent same-origin).
- A background service (`PeriodicTimer`, hourly) deletes images that are not referenced and are older than 24 h.

### 3.8 Configuration
- Environment variables use `__` nesting (`ConnectionStrings__Default`, `Auth__Google__ClientId`, `Admin__Emails__0`, `Storage__ImagesPath`, `Database__MigrateOnStartup`).
- Local secrets come from `dotnet user-secrets`. Secrets are never committed. `deploy/.env.example` documents every variable.
- Logging uses the built-in logger with JSON console output in containers. Personal data such as emails and tokens is never logged.

## 4. Frontend architecture

### 4.1 Rendering mode
**SPA**: `adapter-static` with `fallback: 'index.html'`, and `ssr = false` in the root `+layout.ts`. The app is entirely behind login, so SSR adds nothing and would complicate auth. The build output is static files served by Caddy, which also proxies `/api/*` to the backend, so everything is same-origin. In development, Vite proxies `/api` to `http://localhost:${API_PORT:-5080}`.

### 4.2 Structure and rules
- **Svelte 5 runes only.** Use no legacy syntax and no stores for new state: shared state is classes with `$state` fields, provided through `createContext`. Follow the `svelte-core-bestpractices` skill, and run `svelte-autofixer` (from the `svelte-code-writer` skill) on every new or changed component.
- `src/lib/domain/**` is **pure TypeScript**: no Svelte imports, no DOM, no fetch. Quantities, plurals, formatting, scaling and variant matching all live here and are the most heavily unit-tested code.
- `src/lib/api/`: `schema.d.ts` is generated by `npm run gen:api` from `docs/api/openapi.yaml` and committed. `client.ts` wraps `openapi-fetch`: it adds `X-Requested-With`, converts ProblemDetails into a typed `ApiError { status, code, errors }`, and sends 401 to `/logowanie`.
- Data loading happens in `+page.ts` `load` functions using the client (`fetch` passed through). Mutations call the client and then `invalidate` the relevant keys.
- Routes have Polish URL slugs (the user sees them): `/przepisy`, `/przepisy/[id]`, `/przepisy/nowy`, `/przepisy/[id]/edytuj`, `/przepisy/[id]/warianty/nowy`, `/warianty/[id]/edytuj`, `/lodowka`, `/lodowka/[id]`, `/zaproszenie/[token]`, `/lista-zakupow`, `/profil`, `/logowanie`, `/witaj` (onboarding: nickname).
- **i18n**: every user-facing string lives in `src/lib/i18n/pl/` as typed nested objects, one file per feature (`common.ts`, `errors.ts`, `units.ts`, `recipes.ts`, `variants.ts`, `fridge.ts`, `shopping.ts`, `profile.ts`, `auth.ts`), combined in `index.ts` and imported as `import { t } from '$lib/i18n'` (`t.recipes.ingredients`). No Polish literals in components, so a lint rule (or test) flags quoted strings with Polish diacritics outside `i18n/pl/`. Error codes map to messages in `t.errors`.
- Formatting uses `Intl.NumberFormat('pl-PL')` and `Intl.DateTimeFormat('pl-PL')` through helpers in `lib/domain/format.ts`.

### 4.3 UI and design
- Mobile-first: design at 360 px, then breakpoints `md` 768 px (two columns where useful) and `lg` 1024 px (side navigation rail).
- Design tokens (color, radius, spacing, type scale) are CSS custom properties in `app.css`, and Tailwind theme values reference them. Light and dark themes come from `prefers-color-scheme`.
- Interaction patterns: **bottom sheet** on mobile and popover on desktop for ingredient options and scaling; **dialog** for deduction; **toast** with an action for undo.
- The visual direction is set once in task F0 using the `frontend-design` skill, and recorded in `frontend/DESIGN.md`.
- Accessibility: every interactive element is reachable by keyboard, `bits-ui` primitives provide ARIA, and images need an `alt` (the caption, or the step text as a fallback).

## 5. API standards (summary; details in docs/API.md)
- Base path `/api/v1`, JSON only (multipart only for image upload), camelCase.
- Resource-oriented REST. Sub-resources for ownership (`/fridges/{id}/items`), and action endpoints only for non-CRUD actions (`…/deductions/{id}/undo`, `…/merge`).
- Paging: `?page=1&pageSize=20` (maximum 100) → `{ items, page, pageSize, totalCount }`.
- Concurrency: resources with `version` require it on `PUT`/`PATCH`.
- Errors: ProblemDetails plus `code`.
- **Contract-first**: `docs/api/openapi.yaml` is normative. A change starts in API.md and openapi.yaml in the same commit, then frontend types are regenerated. The backend's generated OpenAPI is compared to the contract in an integration test (paths, methods and status codes), and response bodies in integration tests are validated against the contract schemas.

## 6. Testing strategy

| Layer | Scope | Tools | Location |
|---|---|---|---|
| Backend unit | Core logic: quantity conversion, name normalization, variant diff/resolve/matrix, deduction allocation, plus service rules with an in-memory fake clock; DTO mapping | xUnit v3, Shouldly, NSubstitute | `backend/tests/RecipesList.UnitTests` |
| Backend integration | Real HTTP through `WebApplicationFactory` against **real PostgreSQL (Testcontainers)**; authentication via dev-login; DB reset with Respawn between tests; contract validation | + Testcontainers, Respawn, JsonSchema.Net | `backend/tests/RecipesList.IntegrationTests` |
| Frontend unit | `lib/domain/**` (target ≥ 95 % line coverage), state classes, API client error mapping | Vitest | colocated `*.test.ts` |
| Frontend component | Components rendered in jsdom: interactions, emitted events, a11y roles | Vitest + Testing Library | colocated `*.svelte.test.ts` |
| Frontend integration | Real built app in Chromium with **mocked API** (`page.route`), using fixtures typed from `schema.d.ts`; mobile viewport (Pixel 7) and desktop | Playwright | `frontend/tests/integration` |
| End-to-end (smoke) | Full stack in Docker Compose: login via dev-login, create recipe, add variant, scale, deduct from fridge, shopping list | Playwright | `frontend/tests/e2e` |

Rules:
- **Shared test vectors** live in one directory per kind, with **one file per task** (`<kind>/<TaskID>.json`), and loaders read every `*.json` file in the directory:
  - `quantities/` (unit conversions to base amounts) is asserted by both the backend unit tests and the frontend unit tests;
  - `formatting/` (base amount + class + setting → Polish display string) is asserted by the frontend;
  - `variants/` (base + ops → resolved recipe, and the matrix) is asserted by the backend, and its matrix outputs feed the frontend matching tests;
  - `matching/` (matrix + picks + preferences → match result) is asserted by the frontend.
  Each case has a unique `"id"` prefixed with the task ID (`"F1-onion-2.8"`), and a loader test fails on duplicate ids.
- Test names: C# `Method_Scenario_ExpectedResult`; TS `describe('formatQuantity') / it('rounds 2.8 medium onions to 2 large')`.
- Arrange data with builders (`RecipeBuilder`, `TestData.Recipe()`), with no magic GUIDs inline.
- Every bug fix adds a failing test first.
- Integration tests run in parallel by class. Each test class gets a fresh user via dev-login.
- `npm test` = Vitest. `npm run test:integration` = Playwright with mocks. `npm run test:e2e` needs the compose stack. `dotnet test` runs both backend suites; integration tests need Docker.

## 7. Code style and quality gates
- C#: nullable enabled, `TreatWarningsAsErrors`, `AnalysisLevel=latest-recommended`, file-scoped namespaces, primary constructors where they help, `sealed` by default, `var` when the type is obvious. Formatting is enforced by `.editorconfig` with `dotnet format --verify-no-changes`.
- TS: `strict`, `noUncheckedIndexedAccess`, no `any` (use `unknown` and narrow), ESLint with no warnings, Prettier (tabs, single quotes, width 100).
- Comments explain *why*, not *what*. Public Core APIs get XML docs.
- Commits use Conventional Commits (`feat(recipes): …`, `fix(fridge): …`, `test: …`, `docs: …`).
- **Definition of done** for any task:
  1. the acceptance criteria in the task file are met;
  2. `scripts/verify.sh --agent` is green on a branch rebased onto the latest `main` (build with 0 warnings, all tests, lint, type check, contract and generated-types check; see plan §4.6);
  3. there are no contract deviations (or the contract was updated in the same change and noted in the task report);
  4. no Polish strings appear outside `src/lib/i18n/pl/`, and no English strings appear in the UI;
  5. a short report is appended to the task file under `## Report` (what was done, deviations, follow-ups).

## 8. Local development

Prerequisites: .NET 10 SDK, Node 24 LTS (`nvm use`), Docker Engine with the compose plugin.

| Service | Port |
|---|---|
| PostgreSQL (`deploy/compose.dev.yaml`) | 5432 |
| API (`dotnet run --project backend/src/RecipesList.Api`) | `API_PORT`, default 5080 |
| Frontend dev (`npm run dev` in `frontend/`) | `WEB_PORT`, default 5173 (proxies `/api` → `API_PORT`) |
| Playwright web server (`vite preview`) | `PREVIEW_PORT`, default 4173 |
| Full stack (`docker compose -f deploy/compose.yaml up`) | 8080 (Caddy) |

No port is ever hard-coded: concurrent task agents run with per-task ports (plan §4.3).

Google sign-in needs an OAuth **Web client ID** with authorized JavaScript origins `http://localhost:5173` and `http://localhost:8080` (and the production origin later). When no client ID is configured in Development, the login page shows a dev-login form instead.

## 9. Deployment (self-hosted, prepared but not the first target)
- `deploy/compose.yaml` runs `db` (postgres:18, volume `pgdata`), `api` (multi-stage Dockerfile, `mcr.microsoft.com/dotnet/aspnet:10.0`, non-root, volume `images`) and `web` (Caddy serving `frontend/build`, proxying `/api`, with automatic HTTPS when `SITE_ADDRESS` is a domain).
- Health checks: `GET /api/health` (liveness) and `/api/health/ready` (database reachable).
- Backups are documented in `deploy/README.md` (pg_dump plus the images volume).
