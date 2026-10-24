# RecipesList (Przepiśnik)

A recipe and fridge app: an ASP.NET Core 10 API with PostgreSQL and a SvelteKit SPA. The UI is in Polish; code and docs are in English.

- [PROJECT.md](PROJECT.md): functional spec and decision log
- [TECHNICAL.md](TECHNICAL.md): stack, architecture, conventions, testing
- [docs/API.md](docs/API.md) + [docs/api/openapi.yaml](docs/api/openapi.yaml): the HTTP contract
- [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md): waves, ownership and coordination rules
- [docs/AGENT_DECISIONS.md](docs/AGENT_DECISIONS.md): decisions made by agents rather than the user, and known open issues

## Prerequisites

.NET 10 SDK, Node 24 LTS (`nvm use`), Docker Engine with the compose plugin (the backend integration tests use Testcontainers).

## Quick start: the whole app in Docker

```bash
docker compose -f deploy/compose.yaml up -d --build   # PostgreSQL, the API and Caddy
```

Open http://localhost:8080 once `docker compose -f deploy/compose.yaml ps` shows `api` as healthy and `web` as running. Signing in needs a Google OAuth client ID; [deploy/README.md](deploy/README.md) covers that, HTTPS on a domain, backups and upgrades. `docker compose -f deploy/compose.yaml down` stops it (add `-v` to delete the data).

## Quick start: development

```bash
# 1. Database (postgres:18). If something already listens on 5432, pick another host port:
#    POSTGRES_PORT=5433 docker compose -f deploy/compose.dev.yaml up -d
docker compose -f deploy/compose.dev.yaml up -d

# 2. API on http://localhost:5080 (applies migrations on startup in Development)
dotnet run --project backend/src/RecipesList.Api

# 3. Frontend on http://localhost:5173 (proxies /api to the API)
cd frontend
npm ci
npx playwright install chromium   # once, for the integration tests
npm run dev
```

Open http://localhost:5173. In Development `/logowanie` always shows a dev-login form (email + nickname), and the Google button above it once a client ID is set. To try Google sign-in in development, create the client as in [deploy/README.md](deploy/README.md#google-sign-in) (with `http://localhost:5173` among the origins) and add it to the git-ignored `.env.local`:

```bash
Auth__Google__ClientId=1234-abc.apps.googleusercontent.com
Admin__Emails__0=you@gmail.com      # optional: admin rights for your account
```

With a non-default `POSTGRES_PORT`, point the API at it, for example
`ConnectionStrings__Default="Host=localhost;Port=5433;Database=recipes;Username=recipes;Password=recipes"`.
All configuration variables are listed in [deploy/.env.example](deploy/.env.example).

A second API from the **same** checkout (for example on another `API_PORT` with another database) must not rebuild while the first one runs, because both would race on the build output. Build once, then start the second one with `--no-build`:

```bash
API_PORT=5190 ConnectionStrings__Default="Host=localhost;Port=5432;Database=recipes_second;Username=recipes;Password=recipes" \
  dotnet run --no-build --project backend/src/RecipesList.Api
```

Separate git worktrees have their own build output and are not affected.

## Ports

No port is hard-coded. Every server reads it from the environment, falling back to the defaults:

| Variable | Used by | Default |
|---|---|---|
| `API_PORT` | the API (Kestrel) and the Vite `/api` proxy | 5080 |
| `WEB_PORT` | `npm run dev` | 5173 |
| `PREVIEW_PORT` | `vite preview`, used by the Playwright integration tests | 4173 |
| `POSTGRES_PORT` | host port of the dev database (`deploy/compose.dev.yaml`) | 5432 |

Exported variables win; otherwise both the API and the frontend read a git-ignored `.env.local` at the repository (or worktree) root. This is how several copies of the stack run side by side (plan §4.3), for example:

```bash
printf 'API_PORT=5100\nWEB_PORT=5200\nPREVIEW_PORT=5300\n' > .env.local
```

## Tests and the quality gate

```bash
scripts/verify.sh            # full gate: format, build, all tests, contract, frontend, shared JSON
scripts/verify.sh --agent    # task agents: same, without the MigrationsAreInSync test
scripts/verify.sh --e2e      # full gate, then the E2E smoke suite against the compose stack (Docker, port 8080)
dotnet test backend          # backend unit + integration tests (Docker needed)
cd frontend && npm run check && npm run lint && npm test && npm run test:integration
npm run gen:api              # in frontend/: regenerate src/lib/api/schema.d.ts from openapi.yaml
```

`npm run test:e2e` (in `frontend/`) runs the Playwright suite in `frontend/tests/e2e` against a running stack (`E2E_BASE_URL`, default http://localhost:8080); `--e2e` starts and removes that stack itself (see [deploy/README.md](deploy/README.md)).

`scripts/verify.sh` is the only definition of "green" (plan §4.6). It runs `npm ci` itself when `frontend/node_modules` is missing or older than the lockfile.

## Optional: package caches elsewhere

Nothing requires these, but on a machine with a small root filesystem you can move the package caches, for example:

```bash
export NUGET_PACKAGES=/data/.cache/nuget
export PLAYWRIGHT_BROWSERS_PATH=/data/.cache/ms-playwright
export npm_config_cache=/data/.cache/npm
```
