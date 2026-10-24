# RecipesList (Przepiśnik)

A recipe and fridge app. **The UI is in Polish; code, comments, API and docs are in English.**

## Source of truth (read before changing anything)
- [PROJECT.md](PROJECT.md): functional spec and decision log (D-xx). Do not contradict it; if it is ambiguous, ask or record a new decision.
- [TECHNICAL.md](TECHNICAL.md): stack, architecture, conventions, testing, definition of done. It wins over vendored skills.
- [docs/API.md](docs/API.md) + `docs/api/openapi.yaml`: the HTTP contract. Change both together, or neither.
- [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md): waves, file ownership, coordination rules (§4) and fixed interfaces (§5).
- [docs/COORDINATOR.md](docs/COORDINATOR.md): the coordinator (main session) runbook and **status board**. If you are the coordinator, start there.
- `docs/tasks/<ID>-*.md`: your assignment when you are a task agent (the v1 task files were removed once merged).
- [docs/AGENT_DECISIONS.md](docs/AGENT_DECISIONS.md): every decision an agent made rather than the user, plus known open issues.

## Skills (in `.claude/skills/`, provenance in `SOURCES.md`)
- Svelte work → `svelte-core-bestpractices`, and run `svelte-code-writer`'s autofixer on every changed `.svelte` / `.svelte.ts` file.
- ASP.NET endpoints → `dotnet-webapi`. EF Core queries → `optimizing-ef-core-queries`.
- UI look and feel → `frontend-design` (task F0 only; the others follow `frontend/DESIGN.md`).
- Poking at the running app (screenshots, console) → `webapp-testing`.

## Hard rules
- Never add EF migrations in a feature task. Report the model change instead (plan §4.2).
- Never hand-edit `frontend/src/lib/api/schema.d.ts`. Run `npm run gen:api`.
- No Polish string literals outside `frontend/src/lib/i18n/pl/`. No English text in the UI.
- Every change ships with tests. Bug fixes start with a failing test.
- Never apply migrations to a non-local database, and never push, without the user's approval.

## Commands
Ports come from `API_PORT` / `WEB_PORT` / `PREVIEW_PORT` (defaults below). A task agent uses its slot's ports from `.env.local` (plan §4.3).
```bash
scripts/verify.sh --agent                                 # the quality gate for task agents (full mode for the coordinator)
docker compose -f deploy/compose.dev.yaml up -d          # Postgres
dotnet run --project backend/src/RecipesList.Api          # API on :5080
dotnet test backend                                       # unit + integration (Docker needed)
cd frontend && npm run dev                                # :5173
npm run check && npm run lint && npm test                 # frontend gates
npm run test:integration                                  # Playwright, mocked API
npm run gen:api                                           # regenerate API types from openapi.yaml
```
