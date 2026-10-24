# P1: "Add to the catalog?" prompt and verified-product ranking (full stack)

**Wave v1.1-A · depends on: nothing · slot 2**

## Goal
Typing a product that isn't in the catalog asks whether to add it, and user-added products rank below verified ones in every product search (PROJECT §4.2, §7.1, D-32).

## Read first
PROJECT §4.2, §7 · API §0 (`ProductRef`), §2 · TECHNICAL §3, §4 · `frontend/DESIGN.md`.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Contract (API.md + openapi.yaml together)
- `ProductRef` gains `isVerified: boolean` (always present, read-only; `ProductWrite` doesn't take it). Also add it to the export product type only if the exporter already serializes `ProductRef` (it shouldn't; leave §8 alone otherwise).
- `GET /products` ordering, described in §2: exact normalized match first; then prefix, word prefix, trigram similarity; **within each of those, verified before user-added**; then similarity and name. Without `q`: verified first, then name.

## Owns
`Core/Catalog/Product.cs`, `ProductRules.cs`, `Api/Features/Catalog/**` (except the merge method of `ProductAdmin.cs`, which U1 edits), `ProductConfiguration.cs`, `CatalogSeedService.cs`, catalog tests; frontend `components/catalog/ProductPicker.svelte`, `NewProductDialog.svelte` (+ tests), `components/ui/Combobox.svelte` only if the blur/Enter hook needs it (additive props, keep existing tests green), `i18n/pl/recipes.ts` (only the product keys) or a new `i18n/pl/catalog.ts`, `tests/integration/mocks/` product fixtures (add `isVerified`).

## Touch only if needed
`errors.ts`, `common.ts`, `mocks/index.ts`: additive only. Typed fixtures elsewhere that build a `ProductRef` need `isVerified` added: do that mechanically and list the files.

## Deliverables
- `Product.IsVerified`: the seeder sets it to true; `POST /products` by an admin → true, by anyone else → false; `PUT /admin/products/{id}` → true.
- **No migration.** Report the model change: column `is_verified boolean NOT NULL DEFAULT false`, plus the data step `UPDATE products SET is_verified = true WHERE created_by_id IS NULL OR created_by_id IN (SELECT id FROM users WHERE is_admin)`.
- Search ordering as in the contract, still one query, still using the trigram index; extend the existing search tests.
- `ProductPicker`:
  - When the field loses focus or Enter is pressed with text that doesn't match a result:
    - if the text normalizes to the name of a returned product, select that product;
    - otherwise ask with a `ConfirmDialog`: "„X” nie ma w bazie produktów. Dodać?" with "Dodaj do bazy" / "Nie dodawaj". "Dodaj do bazy" opens `NewProductDialog` prefilled with the name; "Nie dodawaj" clears the field and returns focus to it.
  - Make it a prop (`confirmNew`), on in the recipe editor and the variant editor. Keep the existing "Dodaj produkt" footer.
  - User-added products show the hint "dodany przez użytkownika" next to the category.
- A Playwright integration test of the prompt in the recipe editor (both answers), plus component tests.

## Acceptance
- Search "ceb" with a verified "cebula" and a user-added "cebulka marynowana" (both prefix matches) → "cebula" comes first; an exact user-added match still beats a verified prefix match.
- In the recipe editor, typing "kozieradka" (not in the catalog) and tabbing away → the prompt; "Dodaj do bazy" → the new-product form with the name; "Nie dodawaj" → an empty field.
- Typing "Cebula" and tabbing away → "cebula" is selected without a prompt.
- The admin edit of a user-added product makes it verified.
- `scripts/verify.sh --agent` is green.

## Report

### Summary
- `Product.IsVerified` added. The seeder sets it to true. `POST /products` sets it from `currentUser.IsAdmin`. `PUT /admin/products/{id}` sets it to true.
- `GET /products` ordering is implemented in one query: exact normalized match, then prefix, word prefix and trigram, then verified before user-added within each tier, then similarity and name. Without `q` it is verified first, then name.
- `ProductRef` gained `isVerified` in API.md and openapi.yaml, and `schema.d.ts` was regenerated.
- `ProductPicker` has a `confirmNew` prop, on in the recipe editor's `IngredientRow`. On blur or Enter with unmatched text it does one of two things:
  - If the text normalizes to a returned product's name, it selects that product.
  - Otherwise it shows the `ConfirmDialog`. "Dodaj do bazy" opens `NewProductDialog` with the name filled in. "Nie dodawaj" clears the field and refocuses it.
- User-added products show "dodany przez użytkownika" next to the category in the picker.
- `Combobox` got additive `onCommit` and `clear()` / `focus()`.
- Tests: backend search, create and admin-verify tests plus the seeder assertion; frontend unit tests for the Combobox commit hook, `ProductPicker` and `product-name`; Playwright `product-prompt.spec.ts` covering both answers and the exact-match case.

### Deviations
- The variant editor has no `ProductPicker` in the codebase, so `confirmNew` is wired only in the recipe editor's `IngredientRow` (one added line, outside P1's ownership). Fridge and shopping dialogs keep `confirmNew` off (default false).
- `isVerified` was added to the product DTO records owned by other features: `RecipeProductRefDto` (Recipes), `VariantProductRef` (Variants) and `FridgeProductRefDto` (Fridges). The contract says `ProductRef` always carries it, and those records serialize `ProductRef`. The edits are mechanical: one constructor parameter and one `From` argument each. U1 also edits the recipe and variant DTOs, so expect a trivial merge conflict there.
- The export product type (`ExportModels.cs`) is untouched, per the task.
- Fixtures that build a `ProductRef` got `isVerified: true`: `IngredientRow.svelte.test.ts`, `draft.test.ts`, `mocks/catalog.ts`, `mocks/variants.ts` and `routes/lodowka/harness-odejmowanie/+page.svelte`.
- `ProductPicker` runs its own `GET /products?q=` on commit (not the possibly stale combobox results) to decide whether the text matches a product.
- Cancelling the new-product form leaves the typed text in the field, so the prompt can reappear on the next blur.
- `product-name.ts` uses `ł` for ł because of the no-Polish-literals test.

### Model changes
- `products.is_verified boolean NOT NULL DEFAULT false`. Configured with `HasDefaultValue(false)` in `ProductConfiguration`; no migration was added.
- Data step for the coordinator's migration: `UPDATE products SET is_verified = true WHERE created_by_id IS NULL OR created_by_id IN (SELECT id FROM users WHERE is_admin)`.

### Contract changes
- `ProductRef.isVerified: boolean` (required, read-only) in API.md §0 and openapi.yaml.
- The `GET /products` ordering is described in API.md §2 and in the openapi description. API.md §2 also notes the verified rules for `POST /products` and `PUT /admin/products/{id}`.

### Dependencies added
None.

### Requests for other tasks
- Generate the migration for `is_verified` and run the data step above.
- U1: the merge will touch the `*ProductRef*` records in `RecipeDtos.cs` and `VariantDtos.cs`. Keep both changes.

### Follow-ups
- The picker in the fridge and shopping dialogs could use `confirmNew` if wanted.
