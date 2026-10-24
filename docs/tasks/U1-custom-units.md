# U1: Custom units (full stack)

**Wave v1.1-A · depends on: nothing · slot 1**

## Goal
Recipes, variants and the "Mam:" scaling input can use custom units such as `puszka`, `kostka` or `filiżanka`, taken from a shared, growing catalog (PROJECT §5.6, D-30, D-31).

## Read first
PROJECT §4.2, §5 (all of it, including the new §5.6), §6.1–6.3, §4.7 · API §0, §2, §4, §5, §8, §9 · TECHNICAL §3, §4, §6 · `frontend/DESIGN.md`.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Contract (write it into API.md and openapi.yaml first, both together)
```ts
type Unit = 'g' | 'kg' | 'ml' | 'l' | 'glass' | 'tbsp' | 'tsp' | 'drop' | 'pinch' | 'piece' | 'custom';
type Quantity = {
  value: number; unit: Unit; size?: PieceSize;
  customUnitId?: Uuid;            // required iff unit = 'custom'; size is not allowed with it
  customUnit?: CustomUnitRef;     // read-only: embedded in every response that carries a custom quantity; ignored on write
};
type CustomUnitRef = {
  id: Uuid; name: string; forms: PluralForms;
  productId?: Uuid;               // null = a unit for every product
  measureKind: MeasureKind; amount: number;   // base units of measureKind in one unit (g, ml or medium pieces)
};
type CustomUnitWrite = {
  name: string;                   // 1–40 chars, trimmed
  forms: PluralForms;             // each 1–40 chars
  productId?: Uuid;
  equals: { value: number; unit: Exclude<Unit, 'custom'>; size?: PieceSize };  // "1 <unit> = equals"
};
```
| Method | Path | Body / query | Response | Errors |
|---|---|---|---|---|
| GET | `/units` | `productId?`, `q?`, `limit` (≤ 50, default 50) | `200 CustomUnitRef[]`: units for every product, plus the ones for `productId` when given; product units first, then by name | |
| POST | `/units` | `CustomUnitWrite` | `201 CustomUnitRef`, or `200` with the existing unit (same normalized name and same scope) | 404 `product.not_found`, 400 `unit.invalid` (`equals` can't be converted for that product), 400 `validation.failed` |
| PUT | `/admin/units/{id}` | `CustomUnitWrite` (scope can't change) | `200 CustomUnitRef` | 403 `admin.required`, 404 `unit.not_found`, 400 `unit.in_use` (the amount changes and any ingredient, variant op or shopping item uses it) |

- Stored amount: for a product unit, `IQuantityConverter.ToBaseAmount(equals, product)` (so it is in the product's measure kind); for a unit for every product, the native amount of `equals` (`piece` → `count`, sizes ignored).
- Converting `{ value, unit: 'custom' }`: native amount `value × amount` in the unit's measure kind, then the normal density / piece-weight conversion to the product's kind (§5.2). A product unit used with another product, or a conversion that's impossible → 400 `quantity.unit_incompatible`.
- §8 export: optional top-level `customUnits: { key; name; forms; productKey?; measureKind; amount }[]`; a quantity uses `customUnitKey` instead of `customUnitId`. `formatVersion` stays 1 (old files have no custom units). Import matches by normalized name + scope + measure kind + amount within 1 %; otherwise it creates the unit, adding " (2)", " (3)" on a name clash.
- New error codes in API §9 and `errors.ts`: `unit.invalid`, `unit.in_use`, `unit.not_found`.

## Owns
- Backend: new `Core/Units/**`, `Api/Features/Units/**`, `Configurations/CustomUnitConfiguration.cs`, `backend/http/units.http`, tests for them.
- Backend, quantity-related edits in: `Core/Quantities/**`, `Core/Recipes/RecipeIngredient.cs`, `RecipeInputValidator.cs`, `RecipeService.cs`, `Core/Variants/VariantOps.cs` / `VariantEngine.cs` / `VariantService.cs` (the quantity inside ops and matrix options), `Api/Features/Recipes/RecipeDtos.cs`, `RecipeQueries.cs`, `Api/Features/Variants/VariantDtos.cs`, `Core/Transfer/**`, `Api/Features/Transfer/**`, their entity configurations.
- Frontend: `src/lib/domain/units.ts`, `format.ts`, `scaling.ts`, `plural.ts` (+ tests), new `src/lib/components/units/**` (`UnitSelect.svelte`, `NewUnitDialog.svelte`, a small cached client), `components/recipe-editor/IngredientRow.svelte` (+ tests), `components/recipe-view/IngredientRow.svelte` and the "Mam:" input, `i18n/pl/units.ts`, `tests/integration/mocks/units.ts`.
- `shared/test-vectors/quantities/U1.json`, `shared/test-vectors/formatting/U1.json` (custom-unit conversion and display cases, including the ¼ rounding, the minimum ¼ and the `~` rule).

## Touch only if needed
- `Api/Features/Catalog/ProductAdmin.cs`, merge only: units bound to the source product move to the target (on a name clash in the target's scope, rename the moved one with " (2)"). Task P1 edits the same file at the same time (product verification): keep your change to the merge method.
- `Program.cs` is a hotspot: if `MapUnits` / `AddUnits` aren't wired, add the two lines and say so in the Report.
- `i18n/pl/common.ts`, `errors.ts`, `mocks/index.ts`: additive only.

## Deliverables
- Entity `CustomUnit` (name, normalized name, forms jsonb, product id nullable, measure kind, amount, created by, created at). The unique index is on (normalized name, product id), treating null as its own scope (`NULLS NOT DISTINCT`). `recipe_ingredients.quantity_custom_unit_id` is a FK with `ON DELETE RESTRICT`. Variant ops store `customUnitId` inside their jsonb quantity.
- **No migration.** Report the model change (plan §4.2).
- Everything that returns a quantity embeds `customUnit` (recipe detail, resolved variant, variant detail/ops, matrix options), without an N+1: load the units used by a recipe in one query.
- Frontend: the unit dropdown in the recipe and variant editors lists the built-in units valid for the product, then its custom units, then "+ Nowa jednostka…". That opens `NewUnitDialog`: name, the four plural forms (prefilled from the name; the user corrects them, with an example line "1 puszka · 2 puszki · 5 puszek · ½ puszki"), a "Tylko dla produktu <nazwa>" switch (on by default) and "1 <unit> = [value] [built-in unit]". The "Mam:" input offers the same units.
- `formatQuantityAsEntered` and the scaled formatter handle custom units (D-31). The frontend conversion mirrors the backend and is checked against the shared vectors on both sides.
- Export and import round-trip custom units (`export(import(export(R))) == export(R)`, as B6 tested).

## Acceptance
- Create "puszka" for "pomidory krojone" = 400 g in the editor, use "2 puszki", save → the recipe shows `2 puszki`; scaled ×0.75 → `1 ½ puszki`; ×0.6 → `~1 ¼ puszki`; the base amount is 800 g, and fridge deduction uses it.
- A unit for every product, "filiżanka" = 150 ml, works for milk (volume) and for flour when flour has `gramsPerMl`. Without a density → 400 `quantity.unit_incompatible`, and the UI doesn't offer it.
- A variant can change an ingredient to a custom unit; the matrix option shows it.
- Admin: changing the amount of a used unit → 400 `unit.in_use`; renaming it works.
- Export/import with a custom unit round-trips; importing into a database that already has an identical unit reuses it.
- `scripts/verify.sh --agent` is green.
