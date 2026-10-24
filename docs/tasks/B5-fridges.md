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

## Report

### Summary
All deliverables are done on branch `task/B5-fridges`, which sits on the current `main` (993721d). `scripts/verify.sh --agent` is green: 230 unit tests and 197 integration tests, plus the contract, frontend and Playwright steps.
- **Core** (`Core/Fridges/`, `Core/ShoppingLists/`):
  - `FridgeAccess` runs the membership check. A non-member gets 404 `fridge.not_found`, and a member gets 403 `fridge.owner_required` on owner-only actions.
  - `FridgeService` covers fridges, members and invites. Tokens are 32 random bytes in URL-safe base64 (43 characters) and expire after 7 days.
  - `FridgeItemService` and `FridgeItemQueries` handle items. The one projection for an item with its product and adder runs as a single SQL statement.
  - `DeductionAllocator` is pure. It uses the lowest percent first, then the smaller remaining amount, then input order. Percents are rounded to 2 decimals, clamped to the range 0 to the previous percent, and a package rounded to 0 % is removed.
  - `DeductionService` makes two saves in one `TransactionScope`, as B3 does: the snapshot records each item's `xmin` after the first save.
  - `ShoppingListService` holds the shopping-list logic.
- **Endpoints** (`Api/Features/Fridges/`, `Api/Features/ShoppingLists/`): all 22 routes of API §6 and §7 replace the stubs. DTOs use fridge-specific names (`FridgeProductRefDto`, `FridgeUserRefDto`), as B4 does. I also added `backend/http/fridges.http` and `backend/http/shopping.http`.
- **Undo:** it works only if every non-removed touched item still has the `version` recorded after the deduction, and no removed item exists again under its id. Otherwise it answers 409 `deduction.items_changed`. A second undo answers 409 `deduction.already_undone`. Undo restores every snapshot field, `updatedAt` included, and re-inserts removed items under their old id, so a restored item gets a new `version`.
- **Cook counts:** `Recipe.CookCount` and `Variant.CookCount` go up by one per deduction and are **not** decremented on undo. A cook who undoes a mistaken deduction keeps the count, which is the conservative reading of "undo restores the fridge".
- **Tests:**
  - 11 allocator unit tests: one package, several packages, ties, exact empty, shortfall, no packages, 2-decimal rounding, tiny remainder rounded to 0 and removed, tiny amount, invalid packages.
  - Integration tests cover every acceptance item: the two-user invite flow, expired and revoked invites (404), a non-member getting 404 on all 18 fridge-scoped routes, deduct → undo restoring the exact state, deduct → edit → undo (409), a removed item re-created → undo (409), and the shopping merge and complete flows.
  - `FridgeQueryCountTests` counts SQL statements with an interceptor for 1 row and for 12 rows. Each read runs the same number both times: list fridges 1, detail 2, invites 2, items 2, shopping list 2.

### Deviations
- **Owner removes someone who is not a member:** `DELETE /fridges/{id}/members/{userId}` answers 204 and does nothing. API §9 has no code for a missing member, and DELETE is idempotent.
- **`POST /fridges/{id}/items` with `remainingPercent: 0`:** answers 400 `validation.failed` with `remainingPercent: out_of_range`, because a 0 % item would be removed straight away. Only `PUT` accepts 0, which deletes the item and answers 204.
- **`deduction.empty`:** answers when `lines` is empty, and also when none of the requested products has a package in that fridge. Nothing could be deducted or undone in that case, so no deduction is recorded and no cook count changes. Lines for products that are missing from the fridge, when other lines can be deducted, come back with `deducted 0`, the whole amount as `shortfall` and `items: []`.
- **Deductions with a recipe or variant:** if `recipeId` is not visible to the caller (`VisibilityRules.VisibleRecipes`), the whole deduction is rejected with 404 `recipe.not_found`. A `variantId` must be visible and belong to that recipe, or the answer is 404 `variant.not_found`. A `variantId` without `recipeId` answers 400 (`recipeId: required`). A repeated product answers 400 (`lines[i].productId: duplicate`).
- **Archived recipes (D-27):** these can still be deducted. The deduction does not edit them; only the cook count rises. It works when the cook can still read the recipe: the author, or a user who has a variant of it.
- **`complete { moveToFridge: true }`:** a checked product entry that has neither an amount nor a product `defaultPackageAmount` cannot be turned into a package (a package must be > 0). It **stays on the list** and is not counted in `removed`. Checked text entries are always removed.
- **Default fridge:**
  - Creating a fridge or accepting an invite makes it the user's default **only if the user has none**.
  - When a removed member's default, or a deleted fridge, was the default, it is reset to another fridge the user owns first, then the one they joined earliest, otherwise null.
- **Ordering:**
  - Fridges: oldest first.
  - Members: the owner first, then by join time.
  - Items: by category in catalog order, then product name, then the most-used package first.
  - Shopping list: unchecked first, then catalog products by category in enum order and name, then free-text entries by text.
  - Invites: newest first.
- **Shopping PATCH:** null fields are left unchanged, so an amount cannot be cleared. An `amount` on a text entry, or `text` on a product entry, answers 400 `shopping_item.invalid`. Unchecking an entry does not merge it into another unchecked entry for the same product.
- **Shopping POST:** entries in one batch merge with each other as well as with existing unchecked entries. The response has one element per distinct created or merged entry, in request order. Text entries never merge. When the only problem is the product/text/amount shape, the top-level code is `shopping_item.invalid`; otherwise it is `validation.failed` with field codes.

### Model changes
- **`fridges.owner_id` is no longer unique.** `FridgeConfiguration` now says `HasIndex(x => x.OwnerId).IsUnique(false)`. Before this, EF's relationship discovery (`Fridge.Owner` with `User.DefaultFridge`) left a **unique** index in the model and in `0001_Initial`. That made `POST /fridges` fail with a 500 for every user, since everyone already owns "Moja lodówka", which contradicts PROJECT §8.1. **The coordinator needs to add a migration** that drops and re-creates `ix_fridges_owner_id` as non-unique. `MigrationsAreInSync` fails until then (it is skipped in agent mode). No other entity or configuration changed.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- **Coordinator:** generate the migration for the index change above.
- **B3/B4:** a deduction with `recipeId` changes `recipes.xmin`, and with `variantId` changes `variants.xmin`, because the cook count is stored on the row. An author who has the editor open while someone cooks the recipe gets 409 `concurrency.conflict` on save. To avoid this, either move cook counts out of the versioned row, or accept the conflict and have F4/F5 reload on 409.
- **T00 (test harness):** `ApiClient` has no `PatchJsonAsync`. B5 uses a local helper in `FridgeTestBase`.
- **F6:** `complete` can leave checked entries on the list (see Deviations). After a successful deduction, the undo toast should be offered only for the `id` returned. A deduction answers 400 `deduction.empty` when nothing selected is in the fridge.

### Follow-ups
- Two simultaneous `accept`s by the same user, or an item re-inserted with the same id while an undo runs, hit a primary-key violation and answer 500 instead of being idempotent or 409. This is rare, and could be fixed by catching the unique violation.
- Deductions are never cleaned up; old ones could be pruned (they only serve undo).
