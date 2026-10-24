# B8: Cook counts outside the versioned rows (backend)

**Follow-up · depends on: B3, B4, B5 merged · decision D-28**

## Goal
Cooking a recipe (a deduction, PROJECT §9) must not change the recipe's or variant's `version`. Today `Recipe.CookCount` and `Variant.CookCount` live on rows that use `xmin` as the concurrency token, so a cook while the author has the editor open makes the author's save fail with 409 `concurrency.conflict` (found in B5).

## Read first
PROJECT §6.3 (ranking, step 5.4), §9 and D-28 · TECHNICAL (EF Core conventions, testing) · `docs/tasks/B5-fridges.md` (Report) · `DeductionService`, `RecipeQueries`, `VariantQueries`, `VariantService` (matrix ranking).

## Skills
`optimizing-ef-core-queries`.

## Owns
For this task only: the cook-count entities and their configurations, `CookCount` on `Recipe` / `Variant` (remove it), every place that reads or writes cook counts (`DeductionService`, `RecipeQueries`, `VariantQueries`, `VariantService`, the DTO mapping), and tests under `backend/tests/**/CookCounts/` plus edits to existing tests that reference `CookCount`.

## Deliverables
- Two new entities, each in its own configuration file: `RecipeCookCount (RecipeId PK/FK → recipes, cascade; Count int)` and `VariantCookCount (VariantId PK/FK → variants, cascade; Count int)`. No row version on them.
- A deduction increments the count with one atomic upsert per target (`INSERT … ON CONFLICT (…) DO UPDATE SET count = … + 1`), inside the deduction's existing transaction. It must not load or save the `Recipe` / `Variant` entity.
- Every read of `cookCount` (recipe list/detail, variant list/summary, matrix columns and ranking) gets it through a left join or correlated subquery with `COALESCE(…, 0)`. Query budgets stay constant (the existing statement-count tests must still pass unchanged).
- The HTTP contract does not change: `cookCount` stays where it is today.
- **No migration.** Report the model change. The coordinator writes it, copying the existing counts into the new tables before dropping the old columns.

## Acceptance
- Cook a recipe and a variant, then `PUT` the recipe and the variant with the `version` read *before* the cook → 200, not 409.
- `cookCount` still goes up by one per deduction in every endpoint that returns it, and is not decremented on undo (B5).
- Ranking by cook count in the matrix still works (a test where cook count is the deciding tie-breaker).
- Deleting a recipe or variant removes its count row (cascade).

## Report

### Summary
Cook counts now live in `recipe_cook_counts` / `variant_cook_counts`; `Recipe.CookCount` and `Variant.CookCount` are removed. A deduction upserts the counts (`INSERT ... ON CONFLICT DO UPDATE SET count = count + 1`) through the new `IAppDbContext.IncrementCookCountsAsync`, inside the existing `TransactionScope`. `DeductionService` no longer loads the `Recipe` / `Variant` entities (existence checks only), so their `xmin` is untouched and a save with a pre-cook version returns 200. Reads: variant summaries/detail/list use a correlated subquery (`COALESCE` via `FirstOrDefault`), the matrix folds both counts into its two existing statements (budget unchanged), recipe detail and the resolved variant each add one small query. Tests in `backend/tests/RecipesList.IntegrationTests/CookCounts/` (red commit first): PUT recipe and PUT variant after a cook return 200, neither version changes, counts show in recipe detail, variant detail, variant lists, resolved and matrix, zero when never cooked, cascade on variant and recipe delete. Existing deduction test and the unit-test vector loader were adapted.

### Deviations
- The recipe PUT and the variant PUT are tested in two tests: a recipe that has variants always answers 409 `recipe.has_variants` on PUT unless `deleteVariants` is set (B3 behaviour), so one recipe cannot carry both cases.
- "Ranking by cook count in the matrix": the backend has no ranking code (auto-match is client-side, `VariantMatrixEngine` has no cook-count input); the backend only supplies `cookCount` per matrix column, which is tested. No backend tie-breaker test was possible.
- The upsert is on `IAppDbContext` (`IncrementCookCountsAsync`, implemented in `AppDbContext` with `ExecuteSqlInterpolatedAsync`) because Core does not reference EF Relational.

### Model changes
(No migration written; `MigrationsAreInSync` is expected to fail until the coordinator adds it.)
- New table `recipe_cook_counts`: `recipe_id uuid` PK (`pk_recipe_cook_counts`), FK `fk_recipe_cook_counts_recipes_recipe_id` to `recipes(id)` ON DELETE CASCADE; `count integer NOT NULL`.
- New table `variant_cook_counts`: `variant_id uuid` PK (`pk_variant_cook_counts`), FK to `variants(id)` ON DELETE CASCADE; `count integer NOT NULL`.
- Drop `recipes.cook_count` and `variants.cook_count`. Data copy before the drop: `INSERT INTO recipe_cook_counts (recipe_id, count) SELECT id, cook_count FROM recipes WHERE cook_count > 0;` and likewise `INSERT INTO variant_cook_counts (variant_id, count) SELECT id, cook_count FROM variants WHERE cook_count > 0;`. Verify the generated names with `dotnet ef migrations add`.

### Contract changes
None. `cookCount` stays where it is.

### Dependencies added
None.

### Requests for other tasks
None.

### Follow-ups
- The coordinator writes the migration with the data copy above.
- If a backend-side ranking is ever added, it can read the counts from the matrix columns.
