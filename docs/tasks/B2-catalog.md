# B2: Catalog: products, tags, utensils (backend)

**Wave 1 · depends on: T00**

## Goal
The shared, growing catalog with deduplication, search, admin edit/merge, and the seeder.

## Read first
PROJECT §4.3, §7 · TECHNICAL §3.4 · API §2.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`.

## Owns
`*/Features/Catalog/**`, `backend/src/RecipesList.Core/Catalog/**`, `backend/src/RecipesList.Infrastructure/Seeding/**`, tests, `backend/http/catalog.http`.

## Touch only if needed
`Product`/`Tag`/`Utensil` entity configurations (indexes).

## Deliverables
- `GET /products` search: normalized `q`, prefix matches ranked above trigram similarity (`similarity() > 0.3`), filter by `category`, `limit` ≤ 50. Add a supporting GIN trigram index if T00 missed it (then report the model change).
- `POST /products` with the combination validation (API §2) and dedupe → 409 `product.duplicate` carrying `existingId`.
- `PUT /admin/products/{id}` and `POST /admin/products/{id}/merge`: in **one transaction**, re-point references in `recipe_ingredients`, variant op jsonb (`productId` fields inside `IngredientOps`), `fridge_items`, `shopping_list_items` and `product_preferences` (merging duplicate preference rows by summing weights), then delete the source. Refuse when `measureKind` differs.
- Tags and utensils: search plus idempotent create (an existing normalized match → 200 with the existing tag). `szybkie` is reserved → 400 `tag.reserved`.
- `CatalogSeeder`: an idempotent upsert of `shared/seed/*.json` by normalized name at startup (behind `Seed:Enabled`, default true). It must not overwrite admin edits: insert only when missing.

## Acceptance
- Search: `"smiet"` finds `Śmietanka 30%` and `Śmietana 18%`; prefix hits rank first.
- Dedupe: `"  Cebula "` and `"cebula"` → 409 with `existingId`.
- Merge: a recipe, a variant op, a fridge item, a shopping item and a preference all point at the target afterwards; the source is gone; a mixed `measureKind` → 400.
- Non-admin → 403 `admin.required`.
- The seeder runs twice without creating duplicates; tested with a small fixture seed directory.
- Responses validate against the contract.
