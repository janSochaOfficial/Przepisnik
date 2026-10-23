# F1: Quantities library (pure TypeScript)

**Wave 1 · depends on: T00**

## Goal
Implement `units.ts`, `plural.ts`, `pieces.ts`, `format.ts` and `scaling.ts` in `frontend/src/lib/domain/` with the exact signatures in IMPLEMENTATION_PLAN §5.2.

## Read first
PROJECT §5 (every rule) · TECHNICAL §4.2, §6.

## Owns
`frontend/src/lib/domain/{units,plural,pieces,format,scaling}.ts` and their `*.test.ts`; new vector files `shared/test-vectors/quantities/F1.json` and `shared/test-vectors/formatting/F1.json` (never edit `T00.json`; report disagreements).

## Deliverables
- `toBaseAmount` mirrors the backend `QuantityConverter`. It must pass **every** case in `shared/test-vectors/quantities/`.
- `pluralCategory` and the unit and size plural tables (PROJECT §5.5).
- `bestPieceCombination` (PROJECT §5.4, with the search bound and tie-breakers exactly as written).
- `formatBaseAmount`: every rule in §5.4 for all four display classes and both liquid settings, plus the `~` rule (> 5 % deviation) and the **inventory format** (`context: 'inventory'`). Strings use a decimal comma and the glyphs ¼ ½ ¾ (`1 ½ szklanki`, not `1.5 szklanki`). Unit words come from `src/lib/i18n/pl/units.ts` (create this file; F1 owns it).
- `formatQuantityAsEntered` and `formatIngredientAmount` (factor 1 → as entered; null quantity → null).
- `scaleFactor` and `scaleIngredients`.

## Acceptance
- 100 % of the cases in `quantities/` and `formatting/` pass. Add at least **20 more** formatting cases of your own, covering boundaries: 62.5 ml, 1000 ml vs 1001 ml, 0.625 ml, 0.3 ml pinch, 2.8/1.4/3.5 onions, eggs rounding, `allowHalf`, 12–14 plural exceptions, 22 szklanki.
- ≥ 95 % line coverage for these files (`vitest --coverage`).
- No imports from Svelte or the DOM.

## Report

### Summary
Implemented `units.ts`, `plural.ts`, `pieces.ts`, `format.ts` and `scaling.ts` in `frontend/src/lib/domain/` with the fixed §5.2 signatures, plus `src/lib/i18n/pl/units.ts` (unit words, size words with genitive fraction forms, default `szt.`). All 70 T00 vectors and the new F1 vectors pass (12 quantity cases, 54 formatting cases in `shared/test-vectors/{quantities,formatting}/F1.json`, values derived by hand from PROJECT §5.4, not from the implementation). Line coverage of the five files is 100 %. `scripts/verify.sh --agent` is green. No vector disagreements.

### Deviations
- `scaleFactor` returns 1 for an unusable anchor (recipe base <= 0, user base <= 0 or not finite) instead of throwing or returning NaN. Most conservative for the UI.
- `formatBaseAmount` for `base <= 0` returns `0 g` / `0 ml` / `0 szt.` (not covered by the spec).
- Rounding minimums not in the spec: weight and metric ml/inventory ml are at least 1 (`max(1, ...)`), consistent with the drops/pinches/pieces rules.
- Weight/ml rounding that lands on 1000 (e.g. 999 g, 1020 g) is shown as `1 kg`; the kitchen/metric 1000 ml boundary follows the spec literally (`v > 1000` is litres).
- `formatQuantityAsEntered` shows ¼/½/¾ glyphs for multiples of ¼ and a decimal comma (max 2 digits) otherwise. A `piece` quantity of a product with sizes but no `size` is shown with the product's piece forms (`2 szt.`), not as "średnie".
- `formatIngredientAmount` with factor != 1 and `baseAmount === null` (quantity not convertible) scales the entered value instead and formats it as entered (never approximate).
- The vector schema forbids `gramsPerMl`/`gramsPerPiece` = 0, so zero-guard behaviour is tested in `units.test.ts` rather than in a vector. "22 szklanki" cannot come out of `formatBaseAmount` (glasses stop at 4); it is tested through `formatQuantityAsEntered`, and 22/13/14/23 tablespoons through spice vectors.
- The vector loader tests use `// @vitest-environment node` and read the files with `fs` (`new URL(..., import.meta.url)` is not a file URL under jsdom).

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- None for backend. Consumers (F3+) should import `formatIngredientAmount` / `formatBaseAmount` with `context: 'inventory'` for fridge, shopping list and the deduction dialog, and use `FormattedAmount.approximate` to prepend `~`.

### Follow-ups
- The T00 `stubs.test.ts` now only duplicates the constants test in `units.test.ts` and can be deleted by the coordinator.
- The backend `QuantityConverter` vectors in `quantities/F1.json` are not yet exercised by the backend unit tests unless its loader reads every `*.json` in the directory (it should, per TECHNICAL §6); B-side owners should confirm.
