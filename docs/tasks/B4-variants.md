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

## Report

### Summary
Everything under Deliverables is done, on branch `task/B4-variants`, rebased onto `main` (868ed46, with F1 and F2). `scripts/verify.sh --agent` is green: 209 unit tests, 63 integration tests, 322 Vitest tests (F2's `matching.test.ts` reads `B4.json`) and 6 Playwright tests.
- **Engine** (`Core/Variants/VariantEngine.cs`): `Diff`, `Resolve` and `BuildMatrix` follow D-12, D-20 and API §5.1. Diff output order: modify/replace and remove in base order, then adds in edited order, so every `afterId` points at a row that already exists. `Resolve` throws `InvalidOperationException` naming the op (`ingredient op #n (modify)`, `step op #n (add, Prep)`, …) when a target or `afterId` is missing. `ResolvedRecipeModel.IsQuick` uses `QuickTagRule` (D-09). `MatrixKeys` holds the slot and option key format.
- **Coordinator requests from F2:** (1) `Slot.afterSlotKey` is added and filled in (see Contract changes). (2) Ingredient and step ids that are not in the base become adds and keep the client's id; a duplicated id gets a fresh one. Tests: `Diff_StepIdsNotInTheBase_AreNewStepsKeepingTheirIds` and `Diff_IngredientIdNotInTheBase_IsAnAddKeepingItsId`.
- **Service** (`VariantService`, `SelectionMemory`): CRUD, resolved view, matrix and selection memory. Every read goes through `VisibilityRules`. `VariantQueries.ToSummaryRows` is one SQL statement per list (the caller's ♥ and `baseAvailable` are correlated subqueries), and B7 can reuse it. Delete **and update** drop remembered picks whose slot or option no longer exists among the remaining variants. They also recompute the affected users' `ProductPreference` rows in the same `SaveChanges`.
- **Endpoints** (`Api/Features/Variants/`): all 10 routes of API §5 replace the stubs, with `TypedResults`, metadata and DTOs whose ops use the API shape (no `baseAmount`). Also added: `backend/http/variants.http`.
- **Vectors:** `shared/test-vectors/variants/B4.json` has 13 hand-written cases, all with `edited`, so diff is tested too. They cover two variants adding the same product, the 1 % rule (exactly at the boundary, and keyed by the first-seen amount instead of the base), removals (`none`), add after an added row, utensil and time overrides (including an override of 0), add at the start (ingredient and prep step), note- or optional-only changes, product swaps, step replace/remove/add, a unit change with the same base amount, remove + re-add of a product, key rounding (`…:108.696`), and a mixed case. Every T00 case passes. I found no disagreement with `T00.json`.
- **Tests:** vector-driven resolve, matrix and diff. Exactly one option per column per slot. A seeded property test (5 seeds × 400 cases) checks `Resolve(base, Diff(base, edited)) == edited` up to the ids of added rows, plus matrix invariants. Engine edge cases are covered. Integration tests cover every acceptance item. The matrix with 50 variants runs in ≤ 3 statements (counted with an interceptor).

### Deviations
- **Edits that v1 ops cannot represent are rejected with 400 `validation.failed`.** Each error has a field path. The cases are: reordering base rows (D-20), sending a base row twice, moving a base step to the other section, and clearing a time the base sets (a `null` override means "inherit"). Silently dropping the edit was the less conservative choice.
- **A variant that adds the same product twice** appears in the `p:` slot with its first such row, in resolved order, so that each variant stays in exactly one option.
- **1 % rule:** `|a − k| ≤ 1 % · k`, inclusive, where `k` is the option's first-seen amount. Amounts whose 3-decimal keys collide are merged, so keys stay unique.
- **ResolvedRecipe:** the fields taken from `RecipeDetail` (`id`, `title`, `author`, `visibility`, `isFavorite`, `isArchived`, `cookCount`, dates) describe the **base recipe**. Times, tags (non-system base tags plus `szybkie` from the effective times), utensils, ingredients and steps are the effective ones. When the `szybkie` row is missing from the DB, the tag is left out.
- **Access:** creating a variant on an archived recipe → 404. Updating needs the base to be visible to the author; otherwise → 404 `recipe.not_found` (PROJECT §6.5: "can only delete it"). `resolved` → 404 when the base is not visible, even to the variant's author. Non-author update or delete → 403 when the variant is visible, 404 when it is not. `GET` and `PUT my-selection` on an invisible recipe → 404.
- **Selection PUT:** a malformed slot key → 400 `selection.unknown_slot` (errors `picks.<key>`). Well-formed slots and options that are not in the caller's matrix are dropped silently, with no warning field, since the contract has none. A scaling anchor on a missing slot becomes `null`.
- **Photos in variant steps** must exist and either be the caller's own, already be in the base recipe, or appear in a visible variant of the recipe ("create a variant from the current selection" copies other people's steps). Otherwise → 400 `image.not_owned` or `image.not_found`.
- **Validation codes:** unknown product → field code `product.not_found` under top-level `validation.failed`. Bad quantity → `quantity.invalid`. Incompatible unit → top-level `quantity.unit_incompatible`.
- **Variant names** are unique after trimming and case-sensitive, matching the DB unique index. The check runs before insert, so a simultaneous duplicate still hits the index (500).
- **Shared DTO shapes** are declared under variant-specific names (`VariantProductRef`, `VariantUserRef`, `VariantImageInfo`, …) until common DTOs exist, so they cannot clash with B2/B3 types.
- `afterSlotKey` is **optional** in `openapi.yaml` (not in `required`), so existing typed fixtures stay valid. The server always sends it.

### Model changes
None. No entity, configuration or migration was touched.

### Contract changes
- `Slot.afterSlotKey?: string | null` (API.md §5.1 and `openapi.yaml`, together). It is the base slot key that an added slot's row follows in the first variant that adds it (the nearest base row before it), or null at the start. It is always null for base slots. `frontend/src/lib/api/schema.d.ts` is regenerated.

### Dependencies added
None.

### Requests for other tasks
- **Coordinator / T00 (schema owner):** to let vectors carry `afterSlotKey`, add it as an optional property of `slot` in `shared/test-vectors/schemas/variants.schema.json` (it has `additionalProperties: false`). B4.json leaves it out for now, and the backend tests check it in unit and integration tests.
- **B3:** on `PUT /recipes/{id}` with `deleteVariants: true`, also clean remembered picks and preferences. `SelectionMemory.DropStalePicksAsync(recipe, …)` with the edited ingredients does this. Reuse `QuickTagRule` for the recipe's `szybkie` tag, and consider merging the DTO shapes above.
- **B7:** reuse `VariantQueries.ToSummaryRows` + `VariantSummaryResponse.From` for the variant entries of `/favorites`.
- **F2/F5:** `afterSlotKey` can place added ingredients. Rejected diffs come back as `validation.failed` with paths such as `recipe.ingredients[1].id`, `recipe.cookMinutes` and `recipe.instructionSteps[0].id`.

### Follow-ups
- Recomputing preferences reads all of a user's selections on every PUT. That is fine for v1, but could become incremental later.
- Catch the unique-index violation on a name race and map it to 409 `variant.name_taken`. That needs provider-specific code outside Core.
