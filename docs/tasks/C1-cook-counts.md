# C1: Cook counts: the base's own count, "Ugotowane!" and the 12-hour rule (full stack)

**Wave v1.3 · depends on: V2 merged (both change `VariantService` and the recipe view) · slot 3 · model `sonnet`, effort `high` (a semantic fix of existing counts, a rule enforced in two places that must hold under concurrent requests; the data itself is low-stakes)**

## Goal
PROJECT §9, §9.1, §6.3 (D-42, D-43):
1. **Bug (fix first, starting with a failing test).** The base recipe's cook count (matrix column `base`, the Warianty tab, auto-match criterion 4) is the recipe's total, because every deduction increments `recipe_cook_counts`, even one made while a variant was shown. The base must count only cooks without a variant.
2. A **"Ugotowane!"** button that records a cook without a fridge.
3. A **12-hour rule**: one user's cooks of one recipe count at most once per 12 hours. The button is blocked; a deduction still deducts and only warns.

## Read first
PROJECT §6.3, §6.7, §9 (all, especially §9.1), D-28 · API §4 (`RecipeDetail`), §5 (matrix), §7 (deductions) · `Core/Fridges/DeductionService.cs`, `AppDbContext.IncrementCookCountsAsync`, the cook-count entities and configurations, the matrix query in `Core/Variants/VariantService.cs` · frontend `components/recipe-view/CookActions.svelte`, `components/deduction/**`.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Contract (API.md + openapi.yaml together)
| Method | Path | Body | Response | Errors |
|---|---|---|---|---|
| POST | `/recipes/{id}/cooked` | `{ variantId?: Uuid }` | `200 CookResult` | 404 `recipe.not_found` (not visible), 404 `variant.not_found` (not of this recipe or not visible), 409 `cook.too_soon` (extension `nextAllowedAt`), 409 `recipe.archived` |

```ts
type CookResult = { counted: true; recipeCookCount: number; variantCookCount?: number; nextAllowedAt: DateTime };
// RecipeDetail gains (for the caller):
myLastCountedCookAt?: DateTime | null;   // the caller's last counted cook of this recipe; the client derives the 12-hour state
// DeductionResult gains:
cookCounted: boolean;                    // false when there was no recipeId or the 12-hour rule applied
```
`RecipeDetail.cookCount` stays the **total**. The matrix column `base` returns the base's own count.

## Model changes (report them; no migration, plan §4.2)
- `recipe_cook_counts` gains `base_count int not null default 0` (or an equivalent you justify). Report the **data fix** the coordinator's migration must run: `base_count = greatest(0, count - (sum of the recipe's variant counts))`, since past deductions can't be told apart any more.
- A new table for the 12-hour rule, e.g. `user_recipe_cooks (user_id, recipe_id, last_counted_at)`, PK `(user_id, recipe_id)`, cascading with the user and the recipe.

## Owns
`Core/Fridges/DeductionService.cs` (counting only), a new `Core/Recipes/CookService.cs` (or `Core/Cooking/**`), `AppDbContext.IncrementCookCountsAsync` and the cook-count entities / configurations, the matrix's base count in `VariantService.cs`, `RecipeDetail` mapping (`myLastCountedCookAt`), the cook endpoint in `Api/Features/**`, their tests, `backend/http/recipes.http`; frontend `components/recipe-view/CookActions.svelte`, the deduction dialog's warning in `components/deduction/**`, the keys in `i18n/pl/variants.ts` / `fridge.ts`, mocks and specs.

## Deliverables
- **Counting, one rule for both paths** (`CookService.TryCountAsync(userId, recipeId, variantId)`): inside the caller's transaction, one atomic statement decides whether the cook counts (e.g. `INSERT … ON CONFLICT (user_id, recipe_id) DO UPDATE SET last_counted_at = now WHERE user_recipe_cooks.last_counted_at <= now - interval '12 hours' RETURNING …`), so two simultaneous requests can't both count. If it counts: total +1, and either `base_count` +1 (no variant) or the variant's count +1.
- **Deduction**: always deducts as today; counts through the same rule; returns `cookCounted`.
- **"Ugotowane!"** (`POST /recipes/{id}/cooked`): the same rule; when it does not count → 409 `cook.too_soon` with `nextAllowedAt`. Uses the shown variant like the deduction does (`cook-context.variantId`, `'base'` → none).
- **Frontend**:
  - `CookActions`: a "Ugotowane!" button next to "Odejmij z lodówki". Disabled while `now < myLastCountedCookAt + 12 h`, with the hint "Zapisano jako ugotowane. Ponownie od <godz.>" (add the date when it isn't today; `pl-PL`). On success: toast "Zapisano: ugotowane!", the counts in the view (the Warianty tab) refresh, and the button becomes disabled. A 409 shows the same hint.
  - The deduction dialog shows the 12-hour warning (PROJECT §9.1, exact text) when the rule applies, before the user deducts. After a deduction, the state updates from `cookCounted`.
  - The 12-hour state uses the client clock against the server's timestamp. The server stays the authority (409).
- Polish strings only in `i18n/pl/`.

## Acceptance
- Failing-first test: a deduction with `variantId` → the matrix's `base` column count is unchanged and the variant's count is +1 (fails today).
- Integration: deduction without a variant → base +1 and total +1; with a variant → variant +1 and total +1, base unchanged; a second deduction within 12 h → deducted, `cookCounted: false`, no count change; after 12 h (a fake clock) → counted; "Ugotowane!" → 200, then 409 `cook.too_soon` with `nextAllowedAt`; two concurrent "Ugotowane!" requests → exactly one counted; another user is not limited by mine; undo doesn't change counts or the timestamp; archived → 409; invisible → 404.
- Playwright: press "Ugotowane!" → toast, the button is disabled with the hint; open the deduction dialog within 12 h → the warning is shown and deducting still works; axe light + dark on the action bar and the dialog.
- `scripts/verify.sh --agent` is green.

## Report

### Summary
- **Bug fix (failing test first, d2aa845):** a deduction made while a variant is shown now counts for that variant and the total only; the base's own count (`recipe_cook_counts.base_count`) goes up only for cooks without a variant. The matrix column `base` (Warianty tab, auto-match criterion 4) reads `base_count`; `RecipeDetail.cookCount` stays the total.
- **12-hour rule, one place:** `Core/Recipes/CookService.TryCountAsync(userId, recipeId, variantId)` runs `INSERT ... ON CONFLICT (user_id, recipe_id) DO UPDATE SET last_counted_at = ... WHERE last_counted_at <= now - 12 h` on `user_recipe_cooks` (one atomic statement, so two simultaneous requests cannot both count; the loser waits for the row lock and re-evaluates the `WHERE`), then increments the counts, all in the caller's transaction. `DeductionService` always deducts and counts through it (`DeductionOutcome.CookCounted` → `DeductionResult.cookCounted`; false on an undo, without a recipe, or inside the 12 hours).
- **`POST /recipes/{id}/cooked`** (`CookService.CookAsync`): 200 `CookResult`, 404 `recipe.not_found` / `variant.not_found`, 409 `recipe.archived`, 409 `cook.too_soon` with the extension `nextAllowedAt`. `RecipeDetail.myLastCountedCookAt` for the caller. `backend/http/recipes.http` has the request.
- **Frontend:** `CookActions` has a "Ugotowane!" button (toast "Zapisano: ugotowane!", disabled for 12 hours with the hint "Zapisano jako ugotowane. Ponownie od <godz.>", with the date when it is not today; a 409 shows the same hint; re-enables itself when the 12 hours pass). The deduction dialog shows the PROJECT §9.1 warning while the rule applies; a deduction the server counted (`cookCounted`) starts the 12 hours (timestamp = the deduction's `createdAt`) and bumps the counts in the view. Pure logic in `domain/cook.ts` (+ vitest). Playwright: `tests/integration/cook-counts.spec.ts` (toast and disabled state, hint with date, 409, shown variant sent, warning plus deduct still works, counted vs not-counted deduction, axe light + dark on the action bar and the dialog, 360 px fit).
- Backend integration tests (`CookCounts/CookCountTests.cs`) cover: deduction without a variant (base +1, total +1), with a variant (variant +1, total +1, base unchanged), second deduction inside 12 h (deducts, `cookCounted:false`, no change), after 12 h with a fake clock (counts), no recipe, undo changes neither counts nor timestamp, "Ugotowane!" 200 then 409 with `nextAllowedAt`, deduction then "Ugotowane!" and the reverse, two simultaneous "Ugotowane!" and a simultaneous deduction + "Ugotowane!" (exactly one counted), another user not limited, archived 409, invisible 404, foreign/unknown variant 404, `myLastCountedCookAt` on `RecipeDetail`. Test infrastructure: `ApiFactory.UseFakeClock()` swaps the app's `TimeProvider` (`SwitchableTimeProvider`); `DeductionTests.Undo...` now advances the fake clock by 13 h before its second deduction, since it relied on every deduction counting.

### Deviations
- Files outside C1's **Owns** list, all minimal:
  - `frontend/src/lib/state/selection.svelte.ts` (+ `CookContext` in `components/recipe-view/cook-context.ts` and `routes/przepisy/[id]/+page.svelte`): one method `SelectionState.countCook(variantId, countedAt)` and two context members (`lastCountedCookAt`, `countCook`), so that a cook updates the total, the Warianty tab's count and the 12-hour timestamp without a reload. `reloadMatrix()` alone would not refresh `RecipeDetail.cookCount` / `myLastCountedCookAt`.
  - `frontend/src/lib/components/shell/ActionBar.svelte`: `flex-wrap` added, because three buttons ("Dodaj brakujące do listy", "Ugotowane!", "Odejmij z lodówki") do not fit a 360 px bar (covered by the 360 px spec).
  - `frontend/src/lib/i18n/pl/errors.ts`: one additive key `cook.too_soon`.
  - `backend/tests/.../Infrastructure/ApiFactory.cs`, `SwitchableTimeProvider.cs`, `Fridges/DeductionTests.cs` (shared test infrastructure and an existing test that assumed every deduction counts).
  - `docs/API.md` / `docs/api/openapi.yaml`: the cook endpoint, `CookResult`, `RecipeDetail.myLastCountedCookAt`, `DeductionResult.cookCounted`, the `cook.too_soon` code; `ResolvedRecipe`'s `Omit` list gains `myLastCountedCookAt`.
- `myLastCountedCookAt` is optional and nullable in the contract (as in the task); the server always sends it (`null` when none).
- `cook.too_soon` is added to the `errors.ts` map (generic text) although the button shows the time-based hint instead.
- After a counted deduction the client uses the deduction's `createdAt` (server clock) as the 12-hour start, because `DeductionResult` has no `nextAllowedAt`; the server stays the authority (409).
- The autofixer suggests (not issues) about `setTimeout` in an `$effect` in `CookActions.svelte`; the effect re-enables the button when the 12 hours pass and cannot be a `$derived`. `+page.svelte`'s `tab` effect suggestion predates C1.

### Model changes
No migration was added (plan §4.2). The coordinator's migration (suggested name `C1_CookCounts`) must contain:
1. **`recipe_cook_counts`** gains `base_count integer NOT NULL DEFAULT 0`.
2. **New table `user_recipe_cooks`**: `user_id uuid NOT NULL`, `recipe_id uuid NOT NULL`, `last_counted_at timestamp with time zone NOT NULL`; PK `(user_id, recipe_id)`; index on `recipe_id`; FK `user_id → users(id) ON DELETE CASCADE`; FK `recipe_id → recipes(id) ON DELETE CASCADE`. (Entity `UserRecipeCook`, configuration `UserRecipeCookConfiguration`; let EF generate it.) No backfill is needed: with no row, nothing is blocked.
3. **Data fix for `base_count`** (past deductions can't be told apart any more, so the base gets the total minus what the recipe's variants counted), to run in the same migration after the column is added:
```sql
UPDATE recipe_cook_counts rc
SET base_count = GREATEST(0, rc.count - COALESCE((
    SELECT SUM(vc.count)
    FROM variant_cook_counts vc
    JOIN variants v ON v.id = vc.variant_id
    WHERE v.recipe_id = rc.recipe_id), 0));
```
Variants that were deleted took their `variant_cook_counts` row with them (cascade), so for recipes with deleted variants the fixed `base_count` can be slightly too high; accepted (low-stakes data).

### Contract changes
`docs/API.md` and `docs/api/openapi.yaml`, together: `POST /recipes/{id}/cooked` (`CookRequest`, `CookResult`; 404/409 as in the task, `cook.too_soon` with `nextAllowedAt`), `RecipeDetail.myLastCountedCookAt` (optional, nullable; excluded from `ResolvedRecipe`), `DeductionResult.cookCounted` (required), `MatrixVariant.cookCount` documented as the base's own count for `'base'`, `cook.too_soon` in the error list. `schema.d.ts` was regenerated with `npm run gen:api` (not edited by hand).

### Dependencies added
None.

### Requests for other tasks
- Coordinator: the migration above (`MigrationsAreInSync` will fail until it exists; agents skip it).

### Follow-ups
- Variant ♥/pins unaffected. If cook counts ever need a per-day (not per-12-hours) rule, `CookService.Window` is the single constant on the backend and `COOK_WINDOW_MS` in `domain/cook.ts` on the client.
