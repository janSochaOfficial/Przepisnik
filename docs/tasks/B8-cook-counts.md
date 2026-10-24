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
