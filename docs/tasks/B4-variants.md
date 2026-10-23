# B4: Variants engine and endpoints (backend)

**Wave 1 · depends on: T00 · highest-risk task: be thorough with tests**

## Goal
Implement `IVariantEngine` (diff, resolve, matrix), the variant CRUD, the resolved view, the matrix endpoint, selection memory and product preferences.

## Read first
PROJECT §6 (all of it) · API §5 (all of it) · `shared/test-vectors/variants/T00.json`.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`.

## Owns
`*/Features/Variants/**`, `backend/src/RecipesList.Core/Variants/**`, tests, `backend/http/variants.http`, and the new vector file `shared/test-vectors/variants/B4.json` (never edit `T00.json`; report disagreements).

## Deliverables
- `VariantEngine.Diff(base, edited)` → ingredient ops, step ops, utensil add/remove and time overrides, following the D-12 rules:
  - a row whose id exists in the base and changed → `modify`/`replace`;
  - a base row missing from the edit → `remove`;
  - a row without a base id → `add` with `afterId` = the previous row in the edited order (base or added);
  - reordering base rows alone is **not** represented in v1 (document this; the editor will disallow reordering base rows in variants).
- `Resolve(base, variant)`: apply ops in order and compute the effective times, utensils and `szybkie` tag. A base never changes while it has variants (PROJECT §6.4), so a missing target means corrupted data: throw an `InvalidOperationException` that names the op; there is no stale-op handling.
- `BuildMatrix(base, variants)`: slots, options, keys and the equality rule exactly as in API §5.1; every variant in exactly one option per slot.
- Endpoints from API §5: create/update compute the diff and reject an empty diff (400 `variant.no_changes`); the name must be unique per recipe per author (409 `variant.name_taken`). Creating needs the recipe to be visible (otherwise 404). `GET /variants?author=me` includes variants whose base is no longer visible (`baseAvailable: false`).
- **Visibility:** `GET /recipes/{id}/variants`, the matrix, `resolved` and `GET /variants/{id}` only consider variants from T00's `VisibleVariants(userId)` (the variant author always sees their own variant). `visibility` is part of `VariantWrite`. Summaries and `MatrixVariant` carry `isFavorite` (from `variant_favorites`; for `base`, from `recipe_favorites`), with no N+1.
- Selection memory `GET/PUT /recipes/{id}/my-selection`: drop picks for slots that no longer exist; recompute `ProductPreference` weights (API §5.2) in the same transaction.
- On variant delete: remove its option picks from every `RecipeSelection` that pinned a now-nonexistent option (best effort, in the same transaction).

## Acceptance
- **Test vectors:** every case in `shared/test-vectors/variants/` passes for diff (where `edited` is given), resolve and matrix. Add at least **10 more** cases in `B4.json`, including: two variants adding the same product (one slot); amounts within 1 % sharing an option; a removed ingredient (`none` option); add-after an added row; utensil and time overrides; add at the start (`afterId` null).
- Property-style test: for random small recipes and edits, `Resolve(base, Diff(base, edited))` equals `edited` (up to ids of added rows).
- Integration: create a variant → matrix contains it; a non-author update → 403. Visibility: a private variant by B on A's public recipe is absent from A's matrix and present in B's; a public variant on a fridge-only recipe is invisible to a stranger (404 on the variant too).
- Selection PUT with a swapped product → preference weight 1; the same swap in a second recipe → 2; unpinning → back to 1.
- The matrix query for a recipe with 50 variants runs in ≤ 3 SQL statements.
