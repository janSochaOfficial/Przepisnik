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
