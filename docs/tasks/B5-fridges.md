# B5: Fridges, invites, items, deductions, shopping list (backend)

**Wave 1 · depends on: T00**

## Goal
Everything under `/fridges`, `/invites` and the shopping list.

## Read first
PROJECT §8, §9, §10 · API §6, §7.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`.

## Owns
`*/Features/Fridges/**`, `*/Features/ShoppingLists/**`, `backend/src/RecipesList.Core/Fridges/**`, `backend/src/RecipesList.Core/ShoppingLists/**`, tests, `backend/http/fridges.http`, `backend/http/shopping.http`.

## Deliverables
- Fridge CRUD with owner/member rules. Non-members get **404** (no existence leak).
- Invites: 32-byte URL-safe random token, 7-day expiry, revoke, preview (with `alreadyMember`), idempotent accept.
- Members: the owner removes others, a member leaves; the owner can't leave (400 `fridge.owner_cannot_leave`). If a removed member's `DefaultFridgeId` pointed at this fridge, reset it to one of their remaining fridges, or null.
- Items CRUD, with `remainingAmount` computed and `PUT` with 0 % → delete + 204. Concurrency via `version`.
- **Deduction allocation** as a pure Core function (`DeductionAllocator`): given the items for a product and an amount, consume the lowest `remainingPercent` first; the result is per-item new percents, removals and the shortfall. Persist a `Deduction` with full item snapshots; increment `Recipe.CookCount` and `Variant.CookCount` when given.
- **Undo**: valid only if every touched item still has the `version` recorded after the deduction (removed items must not have been re-created under the same id: re-insert them from the snapshot). Otherwise 409 `deduction.items_changed`. Mark `UndoneAt`. Do not decrement cook counts on undo; document this in the Report.
- Shopping list: list ordering, batch add with the merge rule, PATCH with version, delete, and `complete { moveToFridge }` (API §7).

## Acceptance
- Unit tests for `DeductionAllocator`: one package; several packages (most-used first); exact empty → removed; shortfall; tiny rounding (percent to 2 decimals, never negative).
- Integration: invite flow end to end with two users; expired and revoked → 404; a non-member gets 404 on every fridge route.
- Deduct → undo restores the exact previous state, including removed items; deduct → edit an item → undo → 409.
- Shopping: adding milk 500 ml twice → one item with 1000 ml; `complete` with move → a fridge item at 100 % with that package amount (or the product default when the amount is null; text-only items are just removed).
- Every list endpoint uses a constant number of queries.
