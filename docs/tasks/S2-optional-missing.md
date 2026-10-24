# S2: Ask about optional ingredients when adding missing items (frontend)

**Wave v1.1-B · depends on: P1 merged (only to keep the slots sequential; no code dependency) · slot 3**

## Goal
"Dodaj brakujące do listy zakupów" asks whether missing optional ingredients should be added too (PROJECT §9, D-33).

## Read first
PROJECT §9, §10 · `frontend/DESIGN.md` · the current code: `components/deduction/**`, `src/lib/domain/deduction.ts`, `components/recipe-view/CookActions.svelte`.

## Skills
`svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Owns
`frontend/src/lib/components/deduction/**`, `src/lib/domain/deduction.ts` (+ tests), `components/recipe-view/CookActions.svelte`, `i18n/pl/shopping.ts` (or `fridge.ts`, wherever the deduction texts live), `tests/integration/` deduction / add-missing specs and their mocks.

## Deliverables
- The shopping lines carry whether each one comes from an optional ingredient (extend `DeductionRow` / `toShoppingLines` as needed; keep the pure functions pure and unit-tested).
- Both entry points (the deduction dialog's button and the action bar's "Dodaj brakujące") go through one function that:
  - adds directly when there are no missing optional lines;
  - otherwise asks with a `ConfirmDialog`: "Brakuje też składników opcjonalnych: <nazwy, comma-separated>. Dodać je do listy zakupów?" with "Dodaj też opcjonalne" and "Tylko wymagane" (the default, focused button). Closing the dialog cancels; nothing is added.
  - With only optional lines missing, "Tylko wymagane" adds nothing and shows the existing "nothing missing" toast.
- The success toast counts what was actually added.
- In the deduction dialog, lines the user switched off stay excluded, as they are today; the question only lists optional lines that would be added.

## Acceptance
- A recipe with a missing required "mleko" and a missing optional "rodzynki": the action → question → "Tylko wymagane" adds only milk; repeated with "Dodaj też opcjonalne", it adds both.
- A recipe with no optional ingredients missing → no question.
- Unit tests for the line selection, plus a Playwright integration test of both answers.
- `scripts/verify.sh --agent` is green.

## Report

### Summary
Both entry points (dialog button and action bar) go through `addRowsToShoppingList` in `components/deduction/addMissing.ts`. It adds directly when no optional line is missing. Otherwise it asks via `OptionalPrompt.svelte`, which is mounted imperatively so it works from either entry point. "Tylko wymagane" is focused and adds only required lines (or shows "Niczego nie brakuje."). "Dodaj też opcjonalne" adds all. Escape or an outside click cancels and adds nothing. The toast counts the lines actually added. `DeductionRow` and `NeededIngredient` gained `optional`; `toShoppingLines(rows, {includeOptional})` and `missingOptionalRows` are pure and unit-tested. Playwright covers both answers and cancel. The harness page takes `?optional=1` to add a missing optional "rodzynki".

### Deviations
- A custom AlertDialog (`OptionalPrompt.svelte`) is used instead of `ConfirmDialog`, because `ConfirmDialog` cannot tell Escape (cancel) apart from its cancel button (here "Tylko wymagane").
- The dialog's switches do not affect the add-missing lines, as before; missing rows are disabled anyway.
- Touched `routes/lodowka/harness-odejmowanie/+page.svelte` (not in Owns) to add the `?optional=1` fixture.
- A product on several recipe lines counts as optional only if all its lines are optional.
- Resumed after a machine crash; the work was intact and unchanged.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
None.

### Follow-ups
None.
