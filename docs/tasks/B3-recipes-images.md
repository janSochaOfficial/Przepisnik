# B3: Recipes and images (backend)

**Wave 1 · depends on: T00**

## Goal
Image upload and storage, and recipe CRUD with stable row ids, base-amount computation, the system tag, archiving and search.

## Read first
PROJECT §4, §5.1–5.2 · TECHNICAL §3.4, §3.7 · API §3, §4.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`.

## Owns
`*/Features/Images/**`, `*/Features/Recipes/**`, `backend/src/RecipesList.Core/Recipes/**`, `backend/src/RecipesList.Core/Images/**`, `backend/src/RecipesList.Infrastructure/Storage/**`, tests, `backend/http/recipes.http`, `backend/http/images.http`.

## Touch only if needed
Recipe-related entity configurations.

## Deliverables
- `LocalDiskImageStorage : IImageStorage` + `POST /images` + `GET /images/{id}` (TECHNICAL §3.7: magic-byte check, 10 MB limit, EXIF orientation, 1600/400 px WebP, cache headers). Rate-limit policy `images`.
- `OrphanImageCleanupService` (hourly) that deletes unreferenced images older than 24 h. References are the recipe cover, step blocks (jsonb) and variant `StepOps` (jsonb). Use one SQL query with `jsonb_path_query` (or similar), not a scan in C#.
- `GET /recipes` (filters `q` (trigram on title), `tags` (AND), `author`, `sort`; paging; archived excluded). `GET /recipes/{id}`.
- `POST /recipes` and `PUT /recipes/{id}`:
  - row-id stability rules (API §4), positions from array order;
  - `BaseAmount` via `IQuantityConverter` → 400 `quantity.unit_incompatible` with an `errors` path such as `ingredients[3].quantity`;
  - image-ownership rule → 400 `image.not_owned`;
  - **edit rules (PROJECT §4.5, API §4):** `public` → 409 `recipe.public_locked`; variants exist and `deleteVariants` is false → 409 `recipe.has_variants` with `variantCount` and `foreignVariantCount`; `deleteVariants: true` → delete all variants plus their favourites and remembered picks in the **same transaction** as the update;
  - optimistic concurrency on `version`;
  - system tag `szybkie` added or removed automatically: it applies when at least one of the two times is set and the sum of the set times is ≤ 30. With no times, there is no tag.
- `DELETE /recipes/{id}`: hard delete if there are no variants by other users; otherwise `IsArchived = true` (PROJECT §4.4). Archived recipes stay readable for the author and for the authors of their variants.
- **Visibility:** every read goes through T00's `VisibilityRules` (invisible → 404). `visibility` is set on POST. `PUT /recipes/{id}/visibility` blocks narrowing while foreign variants exist (409 `recipe.visibility_locked`); widening is always allowed.
- DTO fields `visibility`, `isFavorite` (from `recipe_favorites`, with no N+1 in lists), `canEdit`, `variantCount` (visible to the caller), `totalVariantCount` and `foreignVariantCount` (author only), `cookCount`. The favourite endpoints themselves belong to B7.

## Acceptance
- Create → get → update (reorder, edit one ingredient, add one, delete one) keeps the ids of untouched rows.
- Edit rules: a public recipe → 409 `recipe.public_locked`; a private recipe with 2 variants (1 foreign) → 409 with counts 2/1; a retry with `deleteVariants: true` → 200, the variants, their favourites and their picks are gone.
- Visibility: a fridge-only recipe is visible to a fridge mate and 404 for a stranger; after the mate leaves the fridge → 404 for them. Narrowing public → private with a foreign variant → 409 `recipe.visibility_locked`; without one → 200.
- `2 łyżki` of butter (with density) → base 30 ml × gramsPerMl; without density → 400 with the right path.
- `szybkie` appears at 10 + 20 and disappears at 10 + 25.
- A stale `version` → 409.
- Delete with a foreign variant → archived and hidden from the list, `GET` still works; without → 404 afterwards.
- Uploading a renamed `.txt` → 400 `image.invalid_type`; 11 MB → 413; the output is WebP within the size limits (unit-test the image pipeline with small fixtures).
- Orphan cleanup deletes only unreferenced images older than 24 h (fake `TimeProvider`).
- List paging and tag-AND filter are correct; query count is constant per page (check the SQL logs, or assert with an interceptor).
