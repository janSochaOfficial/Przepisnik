# U2: Units on shopping-list items (full stack)

**Wave v1.1-B · depends on: U1 merged · slot 5**

## Goal
A shopping-list item keeps its amount in the unit the user chose, and the user can change that unit (PROJECT §10, D-34). This also removes the "an amount can't be cleared" limitation.

## Read first
PROJECT §5 (including §5.6), §10 · API §0, §7 · the U1 report (custom units, `UnitSelect`) · `frontend/DESIGN.md`.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Contract (API.md + openapi.yaml together, §7)
```ts
type ShoppingListItem = {
  id: Uuid; version: Version;
  product?: ProductRef; text?: string;
  quantity?: Quantity;     // NEW: as chosen by the user (custom units embed customUnit); null = no chosen unit
  amount?: number;         // base units (derived from quantity when it is set); null = no amount
  checked: boolean; addedBy: UserRef; createdAt: DateTime;
};
type ShoppingListItemWrite = { productId?: Uuid; text?: string; amount?: number; quantity?: Quantity };  // amount and quantity are exclusive
type ShoppingListItemPatch = {
  version: Version; checked?: boolean; text?: string;
  quantity?: Quantity;     // sets the unit and amount
  amount?: number;         // sets a base amount and drops the chosen unit
  clearAmount?: boolean;   // true removes the amount and the unit; exclusive with quantity / amount
};
```
Errors: a quantity that doesn't fit the product → 400 `quantity.unit_incompatible`; quantity and amount together, or either with `text` → 400 `shopping_item.invalid`.
Merge: posting a product that has an unchecked item sums the base amounts. The result keeps the existing item's unit, expressed exactly (value = total base ÷ base of one unit, 4 decimals). It takes the new item's unit if the existing one has none, and has no unit if neither has one.

## Owns
`Core/ShoppingLists/**`, `Api/Features/ShoppingLists/**`, `ShoppingListItemConfiguration.cs`, their tests, `backend/http/shopping.http`; frontend `components/shopping/**`, `src/lib/domain/shopping-text.ts` (+ tests), `i18n/pl/shopping.ts`, shopping mocks and specs.

## Touch only if needed
`Core/Quantities/**` for a helper that converts between two quantities of one product (`Convert(Quantity, Unit target, Product)`); if U1 already provides one, reuse it. `Core/Units` usage counting (`unit.in_use`) must include shopping items: add it in U1's check.

## Deliverables
- Columns on `shopping_list_items`: `quantity_value`, `quantity_unit`, `quantity_size`, `quantity_custom_unit_id` (FK RESTRICT). `amount` stays the base amount. **No migration.** Report the model change; existing rows keep `amount` and get no quantity.
- "Move bought items to the fridge" keeps using the base amount.
- `ItemRow` and the share/copy text show `quantity` as entered when present (U1's `formatQuantityAsEntered`), otherwise `amount` in the inventory format.
- The add and edit dialogs have a value field plus `UnitSelect` (built-in units valid for the product and its custom units, "+ Nowa jednostka…").
  - Changing the unit converts the current value: to ¼ for glass/tbsp/tsp/piece/custom units, to 2 decimals for g/kg/ml/l, keeping the piece size.
  - A "Usuń ilość" action clears the amount.
  - Text entries have no amount, as today.
- Fix F6's deviation: amounts are no longer limited to base units in these dialogs.

## Acceptance
- Add "mleko" with `1,5 l` → the list shows `1,5 l`; edit → choose `ml` → the field shows 1500; save → `1500 ml`; choose `szklanka` → 6.
- "Dodaj brakujące" adds 300 ml of milk (no unit) → shown as `300 ml`; adding `1 l` afterwards → `1,3 l` (the existing item had no unit, so the new one's is kept). An existing `2 szklanki` plus `100 ml` → `2,4 szklanki`, displayed per the as-entered rules. Test both.
- A custom unit "puszka" (400 g) on a shopping item: 2 puszki + 200 g merged → `2 ½ puszki`.
- Clearing the amount works and survives a reload.
- `scripts/verify.sh --agent` is green.
