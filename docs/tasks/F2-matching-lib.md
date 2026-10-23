# F2: Variant auto-match engine (pure TypeScript)

**Wave 1 · depends on: T00**

## Goal
Implement `frontend/src/lib/domain/matching.ts` (IMPLEMENTATION_PLAN §5.2), the client side of PROJECT §6.3 and §6.6.

## Read first
PROJECT §6 (all of it) · API §5.1, §5.2 · `shared/test-vectors/variants/` (use the `expectedMatrix` of each case as input fixtures; B4 may add `variants/B4.json` while you work, so the loader picks it up after rebasing).

## Owns
`frontend/src/lib/domain/matching.ts`, `matching.test.ts`, `shared/test-vectors/matching/F2.json` (matrix + picks + preferences → expected `MatchResult` essentials).

## Deliverables
- `matchVariants`:
  - compatibility = agrees with all `source: 'user'` picks;
  - the ranking tuple exactly as in PROJECT §6.3 step 5: remembered-pick matches (all incoming picks, user and auto) → `isFavorite` → the preference score (Σ weights of the variant's `productSwaps` found in `preferences`) → cook count → base first, then older first;
  - the best variant; auto picks for unpinned slots; custom-mix detection and its fallback variant;
  - the per-slot option ordering (compatible first, then support count, then option order in the matrix);
  - the effective ingredient list in display order (base slots in base order, added slots after the base row they follow; use the ingredient's position data from the matrix order).
- `pin`, `clearUserPicks`.
- `draftFromMatch(stepsSource, result)` → a `VariantWrite['recipe']` whose ingredients are the matched ones (keeping base ids for base slots, with no ids for added ones) and whose steps, utensils and times come from `stepsSource`.
- Performance: 50 variants × 40 slots → `matchVariants` < 50 ms (a benchmark test with a generous CI margin, e.g. assert < 150 ms).

## Acceptance
- ≥ 25 cases in `matching/F2.json`: no picks (→ base unless memory says otherwise), one pin narrowing to one variant, pins from two variants (→ custom mix), a remembered auto pick stabilizing the choice when a new variant is added, a favourite beating a higher cook count, a preference tie-break, a cook-count tie-break, `none` options, and an added slot.
- Pure functions: inputs are never mutated (`Object.freeze` the inputs in tests).
- ≥ 95 % line coverage.
