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

## Report

### Summary
- Catalog endpoints implemented (replacing the stubs): `GET/POST /products`, `GET /products/{id}`, `PUT /admin/products/{id}`, `POST /admin/products/{id}/merge`, tags and utensils search plus idempotent create. Code: `Api/Features/Catalog/*` (relational search, admin, merge), `Core/Catalog/ProductRules.cs` (pure validation, name cleaning), `Infrastructure/Seeding/CatalogSeeder.cs`.
- Search ranks prefix, then word-prefix, then `similarity() > 0.3`, all on the normalized name; LIKE wildcards in `q` are escaped.
- Merge runs in one transaction: ingredients, fridge items, shopping items, preferences (conflicts summed, self-swaps dropped), then the source is deleted. The jsonb documents that mention the product (variant `ingredient_ops`, `recipe_selections` picks/scaling, `deductions.lines`) are re-pointed by replacing the product's GUID in their text (v7 GUIDs are unique across entity kinds). A rollback test proves atomicity.
- Seeder: `CatalogSeedService` (hosted, `Seed:Enabled` default true, `Seed:Path` or the nearest `shared/seed`), insert-only by normalized name. Verified against the real S1 files (249 products, 35 tags, 50 utensils): loads cleanly, second run inserts nothing, `szybkie` stays reserved for `POST /tags`.
- Tests: 15 unit, about 45 integration (all pass); `scripts/verify.sh --agent` green after rebasing onto main.

### Deviations
- `PUT /admin/products/{id}` refuses to change `measureKind` of a product already used by ingredients, fridge items or shopping items: 400 `product.invalid_combination` with `errors.measureKind = ["in_use"]` (conservative; the spec is silent).
- Merging a product into itself: 400 `validation.failed` (`targetId: same_as_source`).
- Tag and utensil names are limited to 60 characters (the column length); the contract's `NameWrite` says 80. Longer names give 400 `validation.failed`.
- `category` query is parsed case-insensitively by hand (minimal-API enum binding is case-sensitive); an unknown value gives 400 `request.invalid`.
- Search and merge live in `Api/Features/Catalog` (they need Npgsql/relational APIs that Core must not reference).

### Model changes
None. The trigram GIN indexes on `products`, `tags` and `utensils` already exist.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- Merge rewrites `variants.ingredient_ops` through a raw SQL text replace, so it changes `xmin` (the variant `version`); clients holding an old version get 409 `concurrency.conflict` once and must reload.

### Follow-ups
- The contract's `NameWrite.maxLength` (80) could be lowered to 60 to match tags and utensils.
- Docker image needs `shared/seed` available (or `Seed__Path` set), otherwise the seeder logs a warning and skips.
