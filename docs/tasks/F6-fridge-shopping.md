# F6: Fridges, items, deduction dialog, shopping list and share (frontend)

**Wave 2 · depends on: F0, F1 merged; consumes F5's `CookContext` (if F5 isn't merged, develop the dialog against a test harness page providing the context)**

## Goal
The **Lodówka** and **Lista zakupów** tabs, members and invites, and the deduction dialog launched from the recipe view.

## Read first
PROJECT §8, §9, §10 · API §6, §7.

## Skills
`svelte-core-bestpractices`, `svelte-code-writer`, `webapp-testing`.

## Owns
`frontend/src/routes/lodowka/**`, `routes/lista-zakupow/**`, `frontend/src/lib/components/fridge/**`, `frontend/src/lib/components/shopping/**`, `frontend/src/lib/components/deduction/**`, `frontend/src/lib/domain/deduction.ts`, `frontend/src/lib/domain/shopping-text.ts` (+ tests), `src/lib/i18n/pl/{fridge,shopping}.ts`, `tests/integration/mocks/{fridge,shopping}.ts`, and tests.

## Deliverables
- `/lodowka`: redirect to the default fridge. `/lodowka/[id]`: a fridge switcher (all fridges plus "Nowa lodówka"), items grouped by category, each showing `name — package · zostało X% (~amount)` formatted by F1 with `context: 'inventory'` (the same for the shopping list and the dialog's "available" column), a tap to edit (`Slider` in 5 % steps, or a piece stepper for `count` products; package amount; note; delete), an **add item** sheet (product Combobox with inline create reused from F4's catalog components if merged, otherwise a minimal local one), and search within the fridge.
- Fridge settings sheet: rename, delete (owner), members list with remove/leave, invites (create → copy link / Web Share, list active, revoke).
- `domain/deduction.ts` (pure): `planDeduction(ingredients, fridgeItems)` → rows `{ product, needed, available, status: 'ok'|'short'|'missing', defaultOn }`, totals per product across packages, and `toDeductionRequest(rows, selection)`.
- **Deduction dialog** (opened by F5's action bar button): fridge select (default fridge first), rows with switches and warnings ("Brak w lodówce", "Za mało — jest ~120 ml"), "Odejmij zaznaczone", "Odejmij wszystko", "Dodaj brakujące do listy zakupów"; after success a toast "Odjęto N produktów" with **Cofnij** → the undo endpoint, and the 409 shown as an explained message.
- "Dodaj brakujące do listy" from the recipe action bar (without opening the dialog) adds the missing and short amounts to the default fridge's list and shows a toast.
- `/lista-zakupow`: a fridge switcher; unchecked items first; check-off with optimistic update; add item (product or free text); edit the amount; "Przenieś kupione do lodówki", "Usuń kupione"; **Udostępnij** (Web Share API, hidden when unsupported) and **Kopiuj** (clipboard), both using `shopping-text.ts` → the format in PROJECT §10.

## Acceptance
- Unit: `planDeduction` (multiple packages summed; short; missing; "do smaku" excluded; count products); `shopping-text` (exact format, plural units via F1, unchecked only).
- Playwright with mocks: the fridge add/edit/delete flow; the invite create-and-copy flow; the deduction dialog from a harness or recipe page with a short item → the request lines are correct → undo is called; the shopping list share text is copied (assert the clipboard contents in Chromium with permissions).
- At 360 px the item edit slider is usable and the axe check passes.

## Report

### Summary
- `/lodowka` redirects to the default fridge (or the oldest; an empty state with "Nowa lodówka" when none). `/lodowka/[id]`: fridge switcher, search, items grouped by category (`name`, package, `zostało X % (amount)` in F1 `inventory` format, `~` when approximate), add / edit (5 % slider, or a piece stepper for `count` products; package; note; delete with an undo toast that re-creates the item), settings dialog (rename, members with remove/leave, invites: create copies the link, Web Share when supported, revoke; delete fridge). `/zaproszenie/[token]` (join page) is implemented too.
- `domain/deduction.ts` (`planDeduction`, `toDeductionRequest`, selection helpers, `toShoppingLines`) and `domain/shopping-text.ts`, with unit tests.
- `DeductionDialog` (`components/deduction/`): fridge select, switches, warnings, "Odejmij zaznaczone / wszystko", "Dodaj brakujące do listy zakupów"; success toast "Odjęto N produktów" with Cofnij only for the returned deduction id; 400 `deduction.empty` shown inline; undo 409 codes shown with their `t.errors` text. `addMissingToShoppingList()` does the same without the dialog.
- `/lista-zakupow`: fridge switcher (`?fridge=`), unchecked first, optimistic check-off (per-item request queue so quick taps keep valid versions), add product/free text, edit amount/text, "Przenieś kupione do lodówki", "Usuń kupione" (confirmed), Udostępnij (hidden without Web Share) and Kopiuj. After "move", checked entries that stayed (no amount, no default package) are reported in a toast.
- Dev-only harness `/lodowka/harness-odejmowanie` (same gate as `/ui`) for the Playwright deduction tests.

### Deviations
- Edited `components/recipe-view/CookActions.svelte` (F5), as the coordinator allowed: the placeholders now open the dialog / add missing, reading `getCookContext()`. Its amounts are already scaled, so the dialog gets `baseAmount = scaledBaseAmount` and `factor = 1`; `'base'` variant id is sent as no variant.
- Edited `tests/integration/smoke.spec.ts` (F3): added `'GET /fridges': []` because `/lodowka` now loads the fridge list.
- Added the join page `/zaproszenie/[token]` and the harness route (not listed under Owns, but placeholders/gaps otherwise).
- Item edit saves with an explicit "Zapisz zmiany" button rather than on slider release.
- Amount fields (fridge package, shopping amount) are entered in base units (g / ml / szt.), not in l or kg.
- Shopping amount cannot be cleared (API PATCH ignores null); the edit dialog requires a value > 0.
- No dropdown-menu primitive was needed. Recipe-view "available" quantities use inventory format.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- F5: nothing further (hookup done in `CookActions.svelte`). `getCookContext().variantId === 'base'` is mapped to "no variant" there.

### Follow-ups
- Fridge item deletion "undo" re-creates the item under a new id (no server undo for it).
- Playwright `ui-showcase` specs time out when run with 4 workers while the machine is busy (flaky under load, passed with fewer workers and in the verify run).
