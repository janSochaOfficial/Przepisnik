# Vendored skills

These skills were copied from upstream repositories (2026-10-22) and reviewed before inclusion.
Do not edit them in place; to update, re-copy from upstream and re-review.

| Skill | Upstream | Commit | License | Used for |
|---|---|---|---|---|
| `svelte-code-writer` | [sveltejs/mcp](https://github.com/sveltejs/mcp) `plugins/claude/svelte/skills/` | b2589f8 | MIT | Svelte 5 docs lookup + `svelte-autofixer` (runs `npx @sveltejs/mcp`) |
| `svelte-core-bestpractices` | [sveltejs/mcp](https://github.com/sveltejs/mcp) `plugins/claude/svelte/skills/` | b2589f8 | MIT | Runes, events, snippets, styling rules |
| `dotnet-webapi` | [dotnet/skills](https://github.com/dotnet/skills) `plugins/dotnet-aspnetcore/skills/` | a6bf43a | MIT | Minimal API endpoints, OpenAPI, ProblemDetails |
| `optimizing-ef-core-queries` | [dotnet/skills](https://github.com/dotnet/skills) `plugins/dotnet-data/skills/` | a6bf43a | MIT | EF Core query/index review |
| `webapp-testing` | [anthropics/skills](https://github.com/anthropics/skills) `skills/` | dbd4588 | Apache-2.0 | Ad-hoc Playwright checks of the running app (screenshots, console logs) |
| `frontend-design` | [anthropics/skills](https://github.com/anthropics/skills) `skills/` | dbd4588 | Apache-2.0 | Visual direction for the UI (task F0 design pass) |

Where a skill conflicts with [TECHNICAL.md](../../TECHNICAL.md), TECHNICAL.md wins.
Known deviations:

- `dotnet-webapi` suggests a `.http` file per endpoint group: keep one file per feature in `backend/http/`.
- `webapp-testing` uses Python Playwright for throwaway exploration only; the committed test suites use `@playwright/test` (TypeScript).
