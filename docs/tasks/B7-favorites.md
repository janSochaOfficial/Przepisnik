# B7: Favourites (backend)

**Wave 2 · depends on: B3, B4 merged**

## Goal
The ♥ endpoints and the "Ulubione" list.

## Read first
PROJECT §4.4, §4.6, §6.1 · API §4 (favourite endpoints), §5 (variant favourite endpoints), §5.3.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`.

## Owns
`*/Features/Favorites/**`, `backend/src/RecipesList.Core/Favorites/**`, tests, `backend/http/favorites.http`.

## Touch only if needed
`Program.cs` is pre-wired by T00 for `FavoritesFeature`. If it isn't, add the one `Map`/`Add` line and note it in the Report.

## Deliverables
- `PUT` and `DELETE /recipes/{id}/favorite` and `/variants/{id}/favorite`: idempotent, 404 when the item isn't visible to the caller (`VisibilityRules`).
- `GET /favorites`: recipes and variants merged, newest favourite first, paged in SQL (a `UNION ALL` of the two favourite tables, ordered by `created_at`, then `LIMIT`/`OFFSET`), with **only currently visible** items. Hidden items are filtered out, not deleted. Reuse B3's `RecipeSummary` and B4's `VariantSummary` projections; do not duplicate the mapping.
- Favourites of deleted recipes and variants disappear through the cascade from T00. Add a test for the §4.5 "edit deletes variants" path too.

## Acceptance
- Favourite a recipe and a variant → both are listed, newest first; `isFavorite` is true in `GET /recipes`, `GET /recipes/{id}`, the variant list and the matrix.
- Recipe narrowed to private by its author → it disappears from another user's favourites; widened again → it reappears.
- Paging is correct across the two kinds, and the list endpoint runs in a constant number of queries.
- Favouriting something invisible → 404.

## Report

### Summary
- `PUT`/`DELETE /recipes/{id}/favorite` and `/variants/{id}/favorite`: idempotent, 204; `PUT` returns 404 (`recipe.not_found` / `variant.not_found`) when the item is not visible (`VisibilityRules`). `DELETE` is always 204.
- `GET /favorites`: `FavoriteService.ListAsync` (Core) pages a `UNION ALL` of the two favourite tables in SQL (ordered by `created_at` desc, then id), filtered to currently visible items. The endpoint then loads variant summaries (`VariantQueries.ToSummaryRows` + `VariantSummaryResponse.From`) and recipe summaries (B3's projection) for the page: 2 key statements + 1 variants + 1 recipes, constant for any page size.
- Recipe entries omit `variant` (the contract has `additionalProperties: false`).
- `isFavorite` was already wired by B3/B4; the tests confirm it in recipe list/detail, variant list and matrix.
- Tests: `backend/tests/RecipesList.IntegrationTests/Favorites/FavoritesTests.cs` (listing order, idempotence, 404, narrow/widen, paging across kinds, "edit deletes variants" cascade, recipe delete cascade, statement count). `backend/http/favorites.http` added.
- `AuthFoundationTests.StubEndpoint_...Returns501`: repointed from `GET /favorites` to `GET /recipes/{id}/export` (Transfer stub). The onboarding-403 test still uses `/favorites` (real now, still gated).

### Deviations
- Touched B3 code minimally: `RecipeQueries.ListAsync` projection extracted into the public `RecipeQueries.SummarizeAsync(IQueryable<Recipe>)` (behaviour unchanged) so the favourites list reuses it.
- A favourite is listed only if the item is visible AND (for variant entries) the base recipe is visible, since the entry carries the base `RecipeSummary`. Archived recipes are excluded from recipe entries (as in `GET /recipes`). A variant favourited by its author whose base became invisible is therefore hidden (not deleted).
- Invalid `page`/`pageSize` return 400 `validation.failed` with `out_of_range` field codes, like `GET /recipes`.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- B2/Transfer owner: once `GET /recipes/{id}/export` stops being a stub, the 501 test in `AuthFoundationTests` needs another stub (or should be dropped).

### Follow-ups
- Frontend can use `FavoriteItem.kind` to render "wariant X · przepis Y" from `variant` + `recipe`.
