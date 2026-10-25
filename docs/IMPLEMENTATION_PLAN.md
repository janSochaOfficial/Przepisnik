# Implementation plan

Inputs: [PROJECT.md](../PROJECT.md) (what), [TECHNICAL.md](../TECHNICAL.md) (how), [API.md](API.md) (contract).
Every v1 task is merged. Their task files were removed after the merge; they are in git history (`git show 1200bc8:docs/tasks/<ID>-<slug>.md`), and the decisions from their reports are in [AGENT_DECISIONS.md](AGENT_DECISIONS.md). New tasks go in a new `docs/tasks/`.

## 1. Strategy

1. **Contract first.** API.md and then `openapi.yaml` are frozen before parallel work starts. The frontend builds against mocks typed from the contract, and the backend is tested against the same contract. Neither side waits for the other.
2. **One foundation task (T00) runs alone.** It creates every shared "hotspot" file once: the data model and initial migration, the DI and endpoint registration stubs, the i18n file split, the API client, the test harnesses and the domain-module signatures. After T00, every parallel task works mostly in files it **owns**.
3. **Waves.** Tasks in the same wave are independent and run concurrently, each in its own git worktree and branch. The coordinator (the main Claude session, [COORDINATOR.md](COORDINATOR.md)) merges each branch into `main` as soon as it passes the gate. A task starts as soon as its own dependencies are merged.
4. **Shared test vectors** (`shared/test-vectors/<kind>/<TaskID>.json`, one file per task) keep the duplicated logic (unit conversion) and the cross-layer logic (variant matrix → client matching) consistent.

## 2. Phase 0: environment (manual, done by you)

None of these are installed on this machine yet (checked 2026-10-22: no `dotnet`, no `docker`, Node is v12):

- [ ] .NET 10 SDK
- [ ] Node 24 LTS (via `nvm`/`fnm`; Node 12 is too old for SvelteKit)
- [ ] Docker Engine + compose plugin (needed for Testcontainers and the stack)
- [ ] Google Cloud OAuth 2.0 **Web client ID** with JS origins `http://localhost:5173` and `http://localhost:8080`. This is optional for the first builds, because dev-login works without it.
- [ ] (Optional) Python Playwright for the `webapp-testing` skill: `pip install playwright && playwright install chromium`

## 3. Waves and dependency graph

```
Wave 0  ── T00 Foundation ───────────────────────────────────────────────┐
                                                                         │
Wave 1  ── B1 Auth&Users   B2 Catalog   B3 Recipes&Images   B4 Variants  │ (all parallel)
           B5 Fridges&Deductions&Shopping   S1 Seed data                 │
           F0 Design system   F1 Quantities lib   F2 Matching lib        │
                                                                         │
Wave 2  ── B6 Import/Export   B7 Favourites (both need B3, B4 merged)  │ (parallel)
           F3 Shell/Auth/Profile (needs F0)                              │
           F4 Catalog & Recipe editor (needs F0, F1)                     │
           F5 Recipe view & Variants (needs F0, F1, F2)                  │
           F6 Fridge, Deduction, Shopping list (needs F0, F1)            │
                                                                         │
Wave 3  ── T90 Integration, Docker deployment, E2E, hardening ───────────┘
```

| Task | Title | Wave | Depends on | Est. size |
|---|---|---|---|---|
| [T00](tasks/T00-foundation.md) | Foundation: repo, model, contract, harnesses | 0 | — | L |
| [B1](tasks/B1-auth-users.md) | Google auth, users, settings | 1 | T00 | M |
| [B2](tasks/B2-catalog.md) | Products, tags, utensils, admin merge, seeder | 1 | T00 | M |
| [B3](tasks/B3-recipes-images.md) | Recipe CRUD, images, system tag, archive | 1 | T00 | L |
| [B4](tasks/B4-variants.md) | Variant diff/resolve/matrix, selection memory | 1 | T00 | L |
| [B5](tasks/B5-fridges.md) | Fridges, invites, items, deductions, shopping list | 1 | T00 | L |
| [S1](tasks/S1-seed-data.md) | Polish seed data (products, tags, utensils) | 1 | T00 | S |
| [F0](tasks/F0-design-system.md) | Visual direction, tokens, UI primitives, app shell | 1 | T00 | M |
| [F1](tasks/F1-quantities-lib.md) | Quantities, plurals, formatting, scaling (pure TS) | 1 | T00 | M |
| [F2](tasks/F2-matching-lib.md) | Variant auto-match engine (pure TS) | 1 | T00 | M |
| [B6](tasks/B6-import-export.md) | Recipe JSON export/import | 2 | B3, B4 | M |
| [B7](tasks/B7-favorites.md) | Favourites (♥) endpoints and list | 2 | B3, B4 | S |
| [F3](tasks/F3-shell-auth-profile.md) | Login, onboarding, profile, invite acceptance | 2 | F0 | M |
| [F4](tasks/F4-recipe-catalog-editor.md) | Recipe list/tabs, recipe editor, import/export UI | 2 | F0, F1 | L |
| [F5](tasks/F5-recipe-view-variants.md) | Recipe view, auto-match UI, scaling UI, variant editor | 2 | F0, F1, F2 | L |
| [F6](tasks/F6-fridge-shopping.md) | Fridges, items, deduction dialog, shopping list + share | 2 | F0, F1 | L |
| [T90](tasks/T90-integration-deploy.md) | Merge, migrations, Docker, E2E smoke, hardening | 3 | all | M |

Wave 1 has nine tasks. If running them all at once is too much, start with B3, B4, F0, F1 and F2 (the critical path), then B1, B2, B5 and S1.

## 4. Coordination rules (every agent must follow)

### 4.1 Workspace and lifecycle
- Each task runs in a **git worktree** on a branch named `task/<ID>-<slug>`, created from the **current** `main`.
- **Merge as you go.** There is no "merge at the end of a wave". Each task is merged as soon as it is green, and a wave-2 task starts as soon as **its own** dependencies are merged, without waiting for the rest of wave 1.
- **Rebase before finishing.** Before writing the Report, the agent rebases onto the latest `main` (`git fetch && git rebase main`), resolves conflicts in its **own** files, and re-runs `scripts/verify.sh`. Conflicts in files it doesn't own are left untouched and listed in the Report.
- **Agents never merge into `main`.** The coordinator (see [COORDINATOR.md](COORDINATOR.md)) merges, resolves the remaining conflicts and runs the full gate.

### 4.2 File ownership
- Each task file lists **Owns** (free to create and modify) and **Touch only if needed** (shared files where only additive edits are allowed).
- Hotspot files, and how to edit them:

| Hotspot | Rule |
|---|---|
| `backend/src/RecipesList.Api/Program.cs` | Do not edit. T00 already calls `app.Map<Feature>()` and `services.Add<Feature>()` for every feature; implement them in your feature's files. |
| `AppDbContext`, entity classes, entity configurations | One configuration file per entity (`Configurations/<Entity>Configuration.cs`). Make additive changes only, and only to entities **of your feature**. **Never add migrations**: report the model change in your Report, and the coordinator generates the migration after merging. A change to another feature's entity is a request in the Report, not an edit. |
| `docs/API.md`, `docs/api/openapi.yaml` | Edit only the section(s) **of your own feature**, only when unavoidable, and always both files together; list it under "Contract changes" in the Report. Anything outside your section is a request in the Report. |
| `frontend/src/lib/i18n/pl/<feature>.ts` | Each feature owns its own file. `common.ts` and `errors.ts` are additive-only. |
| `frontend/src/lib/api/schema.d.ts` | Regenerated only (`npm run gen:api`), never edited by hand. On a merge conflict: take either side, then regenerate. |
| `frontend/tests/integration/mocks/<feature>.ts` | Per-feature mock fixtures. `mocks/index.ts` is additive-only. |
| `shared/test-vectors/<kind>/` | **One file per task** (`<kind>/<TaskID>.json`, e.g. `variants/B4.json`). Loaders read every `*.json` file in the directory. Never edit another task's vector file; a disagreement with one is a finding in the Report. |
| Dependencies: `frontend/package.json`, `package-lock.json`, `backend/Directory.Packages.props` | T00 installs everything foreseeable (§4.5). A task that truly needs a new package may add it, as a **separate commit** named `deps: add <package> for <task>`, and lists it under "Dependencies added" in the Report. On a merge conflict the coordinator keeps both additions and **regenerates** the lockfile (`npm install`); lockfiles are never merged by hand. |

### 4.3 Ports and databases for concurrent agents
Agents run at the same time on one machine, so nothing may rely on fixed ports or a shared dev database.

- Every server reads its port from the environment, and the defaults are the standard ports (TECHNICAL §8):
  `API_PORT` (API, default 5080), `WEB_PORT` (Vite dev, default 5173), `PREVIEW_PORT` (the Playwright `webServer` using `vite preview`, default 4173). The Vite proxy targets `http://localhost:${API_PORT}`.
- Each task has a **slot** `n`, and its agent exports `API_PORT=5100+n`, `WEB_PORT=5200+n` and `PREVIEW_PORT=5300+n` (one `.env.local` in the worktree, git-ignored):

| Slot | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 | 13 | 14 | 15 | 16 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Task | T00 / T90 | B1 | B2 | B3 | B4 | B5 | B6 | B7 | S1 | B8 | F0 | F1 | F2 | F3 | F4 | F5 | F6 |

- `PLAYWRIGHT_WORKERS` (optional, same `.env.local` or exported) caps the Playwright workers; the coordinator sets it to `2` while two agents run (COORDINATOR §2), so concurrent suites don't time out under load. Unset means Playwright's default.
- Backend integration tests use **Testcontainers** (random ports, a fresh database), so they need nothing extra.
- If an agent needs a running API (e.g. to try something manually), it uses its own database `recipes_<taskid>` on the shared dev Postgres (`ConnectionStrings__Default=…;Database=recipes_b4`), created with `EnsureCreated`. **Never** use the default `recipes` database from a task.

### 4.4 Database in tests
Integration tests build the schema with `EnsureCreated()` from the current model (which includes the `pg_trgm` and `unaccent` extensions), so a model change works in tests without a migration. A separate test, `MigrationsAreInSync` (trait `Category=Migrations`), checks that there are no pending model changes. Agents skip it (`scripts/verify.sh --agent`); the coordinator and T90 keep it green.

### 4.5 Dependencies installed by T00
- **Backend:** everything in TECHNICAL §1 (Npgsql EF provider, `EFCore.NamingConventions`, `Google.Apis.Auth`, SkiaSharp and its Linux native assets, xUnit v3, Shouldly, NSubstitute, Testcontainers.PostgreSql, Respawn, `Microsoft.AspNetCore.Mvc.Testing`, JsonSchema.Net, `YamlDotNet` for reading the contract) plus `Microsoft.EntityFrameworkCore.Design`.
- **Frontend:** `bits-ui`, `@lucide/svelte` (icons), `tailwindcss` 4 with its Vite plugin, `openapi-fetch`, `openapi-typescript`, `vitest`, `@vitest/coverage-v8`, `jsdom`, `@testing-library/svelte`, `@testing-library/user-event`, `@testing-library/jest-dom`, `@playwright/test`, `@axe-core/playwright`, `ajv` (seed and import schema checks), `@redocly/cli` (contract lint), and ESLint/Prettier/svelte-check with their Svelte plugins.

### 4.6 Quality gate: `scripts/verify.sh`
One script, created by T00, is the only definition of "green":

| Step | Agent mode (`--agent`) | Full (default) |
|---|---|---|
| `dotnet format --verify-no-changes`, `dotnet build` (0 warnings) | ✓ | ✓ |
| `dotnet test` (unit + integration) | ✓ (excluding `Category=Migrations`) | ✓ (all) |
| contract lint (`redocly lint`) + `npm run gen:api`, then `git diff --exit-code schema.d.ts` | ✓ | ✓ |
| `npm run check`, `npm run lint`, `npm test` | ✓ | ✓ |
| `npm run test:integration` | ✓ | ✓ |
| seed and test-vector JSON validation | ✓ | ✓ |
| E2E against the compose stack (`--e2e`) | — | after T90 |

It stops at the first failure and prints which step failed. An agent may not write its Report until `scripts/verify.sh --agent` passes on a branch rebased onto the latest `main`, and the coordinator does not merge until the full `scripts/verify.sh` passes on the merge result. The same script is what a CI pipeline will run later.

### 4.7 Definition of done
See TECHNICAL.md §7. In short: the acceptance criteria are met, `scripts/verify.sh --agent` is green after rebasing, and the Report is written, with these sections: *Summary*, *Deviations*, *Model changes*, *Contract changes*, *Dependencies added*, *Requests for other tasks*, *Follow-ups*.

## 5. Interfaces fixed by T00 (so wave-1 tasks can work in parallel)

### 5.1 Backend (Core)
```csharp
public interface ICurrentUser { Guid Id { get; } bool IsAdmin { get; } }
public interface IAppDbContext { /* DbSet<T> for every entity */ Task<int> SaveChangesAsync(CancellationToken ct); }
public static class NameNormalizer { public static string Normalize(string s); }                // implemented in T00
public sealed class VisibilityRules(IAppDbContext db) {                                           // implemented in T00 (PROJECT §4.4, §6.1)
  IQueryable<Recipe> VisibleRecipes(Guid userId);   // author || public || (fridges && shares a fridge with author)
  IQueryable<Variant> VisibleVariants(Guid userId); // recipe visible && same rule applied to the variant's author/visibility; author always sees own
}
public interface IQuantityConverter { decimal? ToBaseAmount(Quantity? q, Product product); }     // implemented in T00 (vectors)
public interface IImageStorage { Task<StoredImage> SaveAsync(Stream s, CancellationToken ct); Stream? Open(Guid id, ImageSize size); Task DeleteAsync(Guid id, CancellationToken ct); }  // implemented in B3
public interface IVariantEngine {                                                                 // implemented in B4
  VariantDiff Diff(Recipe baseRecipe, RecipeDraft edited);
  ResolvedRecipeModel Resolve(Recipe baseRecipe, Variant? variant);
  VariantMatrixModel BuildMatrix(Recipe baseRecipe, IReadOnlyList<Variant> variants);
}
// Domain exceptions: NotFoundException(code), ForbiddenException(code), ConflictException(code), DomainValidationException(code, errors?)
```

### 5.2 Frontend (`src/lib/domain`, stubs throw `new Error('not implemented')` until F1/F2)
```ts
// units.ts (F1)
export const ML_PER: Record<'glass'|'tbsp'|'tsp'|'drop'|'pinch'|'ml'|'l', number>;
export const SIZE_FACTOR: Record<PieceSize, number>;
export function toBaseAmount(q: Quantity, p: ProductRef): number | null;            // mirrors backend; null if incompatible
// plural.ts (F1)
export function pluralCategory(n: number): 'one'|'few'|'many'|'fraction';
// pieces.ts (F1)
export type PieceCombo = { large: number; medium: number; small: number } | { fractionOfMedium: 0.25 | 0.5 };
export function bestPieceCombination(t: number): PieceCombo;
// format.ts (F1)
export type FormatOptions = { liquidUnits: LiquidUnits; context?: 'recipe' | 'inventory' };   // inventory ⇒ PROJECT §5.4 "Inventory format"
export type FormattedAmount = { text: string; approximate: boolean };
export function formatBaseAmount(base: number, p: ProductRef, o: FormatOptions): FormattedAmount;
export function formatQuantityAsEntered(q: Quantity, p: ProductRef): string;
export function formatIngredientAmount(i: Ingredient, factor: number, o: FormatOptions): FormattedAmount | null; // null ⇒ "do smaku"
export function formatNumberPl(n: number, maxFractionDigits?: number): string;
// scaling.ts (F1)
export function scaleFactor(anchorRecipeBase: number, anchorUserBase: number): number;
export function scaleIngredients(list: Ingredient[], factor: number): (Ingredient & { scaledBaseAmount: number | null })[];

// matching.ts (F2)
export type MatchInput = { matrix: VariantMatrix; picks: Record<string, Pick>; preferences: ProductPreference[] };
export type RankedOption = { option: SlotOption; compatible: boolean; supportCount: number; selected: boolean; source?: 'user'|'auto' };
export type MatchResult = {
  bestVariantId: string;                       // uuid or 'base'
  isCustomMix: boolean;
  compatibleVariantIds: string[];
  picks: Record<string, Pick>;                 // complete: user pins + auto picks for every slot
  options: Record<string, RankedOption[]>;     // per slot, ordered for the sheet
  ingredients: Ingredient[];                   // effective ingredient list in display order ("none" omitted)
};
export function matchVariants(input: MatchInput): MatchResult;
export function pin(picks: Record<string, Pick>, slotKey: string, optionKey: string): Record<string, Pick>;
export function clearUserPicks(picks: Record<string, Pick>): Record<string, Pick>;
export function draftFromMatch(stepsSource: ResolvedRecipe, result: MatchResult): VariantWrite['recipe'];
```

## 6. Critical path and risks

| Risk | Mitigation |
|---|---|
| Variant model complexity (diff, matrix) | B4 is test-vector driven (`shared/test-vectors/variants/`). F2 consumes matrix vectors produced by B4's cases. T00 writes the first 5 vectors by hand. The edit rules (PROJECT §4.5) remove the stale-op problem entirely. |
| Visibility leaks (a private recipe shown to the wrong user) | One `VisibilityRules` implementation (T00) with a 3 × 3 combination test matrix. Every task's integration tests include a "stranger gets 404" case. |
| Frontend and backend unit math drift | Shared `quantities/` vectors are asserted on both sides. |
| Parallel merge conflicts | T00 pre-creates hotspots; ownership is per task; one vector file per task; dependencies pre-installed and lockfiles regenerated, never hand-merged; migrations only by the coordinator; merge as you go plus rebase before finishing (§4). |
| Agents colliding at runtime (ports, dev DB) | Per-task port slots from the environment and per-task databases; integration tests use Testcontainers (§4.3). |
| "Green" meaning different things to different agents | One gate, `scripts/verify.sh`, used by agents (`--agent`), the coordinator (full mode) and future CI (§4.6). |
| Google OAuth setup blocks testing | Dev-login in Development/Testing; the frontend shows a dev-login form when no client ID is configured. |
| Testcontainers needs Docker | Phase 0 prerequisite. Backend unit tests run without Docker. |
| Scope creep in UI polish | F0 fixes the design once. UI tasks reuse primitives and do not restyle. |

## 7. Launching agents and merging

The full coordinator procedure (picking ready tasks, the spawn prompt, merge steps and conflict rules) is in **[COORDINATOR.md](COORDINATOR.md)**.
