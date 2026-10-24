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
