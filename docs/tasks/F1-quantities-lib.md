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
