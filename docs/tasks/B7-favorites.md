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
