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
