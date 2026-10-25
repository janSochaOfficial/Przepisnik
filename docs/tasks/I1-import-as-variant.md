# I1: Import an existing recipe as variants (full stack)

**Wave v1.1-B · depends on: U1 merged (export format with custom units), S2 merged (slot order) · slot 4**

## Goal
Importing a file whose recipe already exists offers adding it as variants of that recipe (PROJECT §4.7, D-35).

## Read first
PROJECT §4.4, §4.5, §4.7, §6 (all of it) · API §4 (import), §5, §8 · TECHNICAL §3 · the B4 variant code (`Core/Variants/**`) and the B6 import code (`Core/Transfer/**`, `Api/Features/Transfer/**`).

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Contract (API.md + openapi.yaml together, in §4 next to import)
| Method | Path | Body / query | Response | Errors |
|---|---|---|---|---|
| GET | `/recipes/import-matches` | `title` (1–150) | `200 RecipeSummary[]`: visible, non-archived recipes whose normalized title equals the normalized `title`; the caller's own first, then newest; at most 10 | 400 `validation.failed` |
| POST | `/recipes/{id}/variants/import` | `RecipeExport` (same limits and rate limit as `/recipes/import`) | `201 VariantImportResult` when at least one variant was created, otherwise `200` | 404 `recipe.not_found`, 409 `recipe.archived`, 400 / 413 as for import |

```ts
type VariantImportResult = { recipeId: Uuid; variants: VariantSummary[]; skipped: number };
```

## Owns
`Core/Transfer/**`, `Api/Features/Transfer/**`, a new pure `Core/Variants/ImportAlignment.cs` (+ unit tests), transfer integration tests, `backend/http/transfer.http`; frontend `components/recipe-editor/ImportDialog.svelte`, `transfer.ts` (+ tests), the import keys in `i18n/pl/recipes.ts`, the import mocks and specs.

## Touch only if needed
`Core/Variants/VariantService.cs` / `VariantEngine.cs`: only to expose the existing "diff an edited full copy against the base" and "resolve ops against a recipe" entry points for reuse (no behaviour change). If they can't run on an in-memory recipe, add a small overload; don't duplicate the diff.

## Deliverables
- **Matching.** `import-matches` uses `NameNormalizer` on both sides (`normalized_title`), composing with `VisibilityRules.VisibleRecipes`.
- **Import as variants**, in one transaction:
  1. Match or create products, tags, utensils, custom units and images exactly as the normal import does (reuse it; don't fork it).
  2. Resolve every file variant against the file's recipe in memory (the file's ops are against its own recipe).
  3. For the file recipe ("Wersja @<authorNickname>") and each resolved file variant (its own name), build an edited full copy of the **target** with `ImportAlignment`:
     - **Ingredients:** a longest common subsequence on product id. Matched rows keep the target row's id and take the file's quantity/note/optional; unmatched target rows are removed; unmatched file rows are added after the nearest preceding matched row.
     - **Steps,** per section: an LCS on normalized block text (trimmed, whitespace collapsed); unmatched steps between two anchors are paired by position as replacements; the rest are removed or added.
     - **Utensils:** set difference. **Times:** an override when they differ. Tags and the cover are not part of variants.
     - Row order is never changed (D-20).
  4. Diff with the existing variant diff. An empty diff counts as `skipped`. Name clashes among the importer's variants of the target get " (2)", " (3)" (the same rule as B6). Each variant is private, owned by the importer, and its description carries the import footer.
- **Frontend.** `ImportDialog` parses the chosen file locally (JSON; invalid → the existing error) and calls `import-matches` with `recipe.title`.
  - No matches → the normal import.
  - Otherwise a choice step "Ten przepis już istnieje": one "Dodaj jako wariant" option per match (title, @author, visibility label), "Importuj jako nowy przepis" and "Anuluj".
  - After a variant import, it navigates to `/przepisy/{recipeId}?wariant={first created id}`. If everything was skipped, it stays and shows a toast "Te warianty już istnieją (pominięto N)".

## Acceptance
- Export my recipe R, import that file again → the choice step lists R. "Dodaj jako wariant": the file recipe is identical, so it is skipped; its variants that equal existing ones are skipped too; the result is 200 with `skipped` > 0 and no variants.
- Import a file whose title matches R but whose ingredients differ (one changed amount, one extra row, one missing row, steps reworded) → one variant whose resolved recipe equals the file recipe, apart from row order constraints and ids. Add an integration test that resolves it and compares.
- A file variant becomes a variant of R whose resolved form equals the file variant's resolved form.
- An archived target → 409; a target not visible → 404.
- Unit tests of `ImportAlignment` (LCS ties, all rows different, empty sections, duplicate products).
- `scripts/verify.sh --agent` is green.

## Report

### Summary
- **Contract** (API.md §4 + openapi.yaml, together): `GET /recipes/import-matches?title=` → `RecipeSummary[]` (visible, non-archived, `normalized_title` equal to `NameNormalizer.Normalize(title)`, own first, then newest, ≤ 10; 400 `validation.failed` for a blank or > 150-character title) and `POST /recipes/{id}/variants/import` (`RecipeExport` → `201`/`200 VariantImportResult`, same 25 MB limit and `import` rate-limit policy; 404 `recipe.not_found`, 409 `recipe.archived`). New schema `VariantImportResult`; a paragraph in §4 describes the alignment and skip rules.
- **Core**: new pure `Core/Variants/ImportAlignment.cs`: `Align(targetResolved, sourceResolved) → RecipeDraft` (ingredients: LCS on product id; steps per section: LCS on `StepText` = text blocks joined, trimmed, whitespace collapsed, then positional pairing between anchors; utensils = the source's set; times as described below), `HasSameContent` (id-free equality of two resolved recipes) and `StepText`. `VariantService` / `VariantEngine` are unchanged: the engine's public `Resolve` and `Diff` already run on in-memory `Recipe` / `Variant` objects.
- **API**: `RecipeImporter` became `partial`; the transaction / photo-cleanup / error-mapping wrapper (`InTransactionAsync`) is shared by both imports, and the new `RecipeImporter.Variants.cs` reuses `ResolveProductsAsync`, `ResolveUnitsAsync`, `ResolveUtensilsAsync`, `StoreImagesAsync`, the op mappers and `UniqueName` unchanged. It builds the file's recipe and variants in memory, resolves each file variant against the file's recipe, aligns every candidate with the target, diffs it with `IVariantEngine.Diff`, and stores the created variants (private, owned by the caller, import footer in the description) in one transaction. `TransferFeature` maps both endpoints; `backend/http/transfer.http` has examples.
- **Frontend**: `transfer.ts` gained `matchTitle`, `findImportMatches`, `importAsVariants`, and `importRecipe` now takes the parsed file. `ImportDialog` parses the file locally (invalid JSON → the existing `import.invalid_format` message), looks for matches, and shows the "Ten przepis już istnieje" step: one "Dodaj jako wariant" per match (title, @author, visibility label), "Importuj jako nowy przepis" and "Anuluj". When variants are created it navigates to `/przepisy/{recipeId}?wariant={first}`; when everything is skipped it stays and shows "Te warianty już istnieją (pominięto N)". The new keys are in `i18n/pl/recipes.ts` (`transfer.*`).
- **Tests**: `UnitTests/Variants/ImportAlignmentTests.cs` (13: identical recipe, the acceptance mix, LCS tie, all rows different, anchors and gaps, empty sections, duplicate products, add positions, utensils/times, `StepText`, `HasSameContent`). `IntegrationTests/Transfer/ImportAsVariantsTests.cs` (12): matching (normalization, visibility, archived, order, 400s); re-importing my own export gives 200 with `skipped` 3, no variants and no duplicate photos; a changed file resolves equal to the file recipe (compared field by field, and the matched row keeps the target's id); file variants resolve equal to their source; new step photos are stored for the importer and the cover is not; " (2)" on a name clash; 409 archived; 404 not visible; 400 invalid file creates nothing. Vitest `transfer.test.ts` (`matchTitle`). Playwright `import-variants.spec.ts` (no matches → normal import; the request title and the listing; add as variant → URL with `?wariant=`; all skipped → toast, stays; import as new; cancel; non-JSON file; axe light/dark on the choice step) plus `mocks/transfer.ts`.

### Deviations
- **Skipping goes beyond "empty diff".** The acceptance criteria require the file's variants that equal existing ones to be skipped, but their diff against the target is not empty. A candidate is therefore skipped when its diff is empty **or** its resolved form equals (`HasSameContent`, ignoring ids) the target, an existing variant of the target **visible to the importer**, or a variant created earlier in the same import.
- **Photos.** A file photo that is byte-identical to the stored full-size image (exactly what export embeds) of a step photo of the target, or of a visible variant of it, reuses that image id. Without this, re-importing an export with step photos could never be "identical". Other photos are stored, owned by the importer, only if a created variant uses them, so skipped candidates leave no orphans. The cover is ignored (variants have no cover). This is the only place where image handling differs from the normal import, which stores every referenced photo.
- **Times.** A time that the file leaves empty while the target sets it keeps the target's value: a variant override of `null` means "inherit", so the engine cannot express clearing it (it would reject the draft).
- **Steps.** The start and end of a section count as anchors, so with no common step at all, steps are still paired by position as replacements. Image blocks are ignored by the alignment key; they do count when the matched step's blocks are compared (a changed photo becomes a `replace`).
- **LCS ties** keep the earliest target row available for matching, so the source row is the one treated as added (documented on `ImportAlignment`).
- Variant validation runs through the importer, not through `VariantService.DiffAsync` (which is private and needs a `VariantRecipeInput`). The file recipe's shape is checked with `RecipeInputValidator` (the same limits the normal import enforces through `RecipeService`), and quantities with the importer's `BaseAmount` check. Any failure is 400 `import.invalid_format`, as for the normal import.
- `201` sends `Location: /api/v1/variants/{first created id}` (the task gives no location).
- **Frontend:** a file without a usable title (missing, or not 1–150 characters after trimming) skips matching and goes straight to the normal import, so the server reports it as before. After a variant import with created variants, a success toast is shown ("Dodano warianty z pliku.", plus the skipped count when > 0). The task specified only the navigation. "Anuluj" is the dialog's footer cancel button.
- I edited the existing import test in `recipe-editor.spec.ts` (an import spec, which this task owns) to mock `GET /recipes/import-matches` with `[]`. Note for mocks: `GET /recipes/:id` also matches `/recipes/import-matches`, so the transfer handlers must be spread first (documented in `mocks/transfer.ts`).
- The Svelte autofixer suggests removing the `eslint-disable-next-line svelte/no-navigation-without-resolve` before the `?wariant=` navigation, but ESLint fails without it. This is the same pattern as `warianty/nowy` and `warianty/[id]/edytuj`.

- Gate: the first `verify.sh --agent` run (before U2 was merged) failed with Playwright timeouts under load from the concurrent agent (6 axe/debounce tests, 3 of them existing ones). The same specs passed with `--workers=2`. After rebasing onto main with U2 in it (no conflicts; `schema.d.ts` regenerated with no diff), `scripts/verify.sh --agent` is green, with no timeouts raised.

### Model changes
None.

### Contract changes
API.md §4: two table rows, an "Import as variants" paragraph and `VariantImportResult`. openapi.yaml: paths `/recipes/import-matches` (GET, `ListImportMatches`) and `/recipes/{id}/variants/import` (POST, `ImportRecipeAsVariants`, tag `transfer`), and schema `VariantImportResult`. `schema.d.ts` was regenerated.

### Dependencies added
None.

### Requests for other tasks
- Coordinator: record the deviations above (skip rule, photo reuse, kept times, section-edge anchors) in `docs/AGENT_DECISIONS.md` (not owned by this task).

### Follow-ups
- Matching compares the full normalized title for equality. Near-duplicates ("Naleśniki" vs "Naleśniki z serem") are not offered.
- The photo check reads every step photo of the target and its visible variants from storage, but only when the file has step photos. That is fine at recipe scale; a stored content hash would avoid it.
