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
