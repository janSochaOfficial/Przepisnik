# I2: "Format pliku" help page for import (frontend)

**Wave v1.2 · depends on: nothing · slot 2 · model `haiku`, effort `low` (a static, low-risk page; the content must match API.md §8 exactly)**

## Goal
The import dialog links to a Polish page that explains the import/export JSON format (PROJECT §4.7, D-40).

## Read first
PROJECT §4.7, §5.6 · API §8 (the whole `RecipeExport` format, including custom units) and the `RecipeExport` schema in `docs/api/openapi.yaml` · `frontend/DESIGN.md` · `src/lib/components/recipe-editor/ImportDialog.svelte`.

## Skills
`svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Owns
A new route `src/routes/przepisy/import/format/+page.svelte` (+ `+page.ts` if needed), a new `i18n/pl/import-format.ts` (registered in the i18n index additively), a new spec `tests/integration/import-format.spec.ts`.

## Touch only if needed
`components/recipe-editor/ImportDialog.svelte`: only to add the "Format pliku" link (opens the page in a new tab, `target="_blank" rel="noopener"`, so the dialog keeps its state) · the import keys in `i18n/pl/recipes.ts`: one key for the link label.

## Deliverables
- The page (`<Page title="Format pliku importu">`, back to `/przepisy`), in Polish, with sections:
  1. what the file is (one recipe, its variants, photos embedded; up to 25 MB; `formatVersion: 1`);
  2. the top-level structure, then a table per object (recipe, ingredient, quantity and units, steps and blocks, variant and its operations, custom units, images): field, meaning, required or not, limits. Field names stay in English as in the file (`<code>`), descriptions in Polish;
  3. how import matches products, tags, utensils and units by name, and what "Dodaj jako wariant" does (PROJECT §4.7, short);
  4. a short, **valid** complete example (one recipe with 2 ingredients, one step, one variant, no photos) in a `<pre>`, with a "Kopiuj" button (clipboard; a toast on success).
- The example lives in a `.json` test fixture or a TS constant that a Vitest test validates against the import schema the app already uses (`ajv`, the shared/openapi schema), so it can't drift from the format.
- Accessible: headings in order, tables with `<th scope>`, the example is reachable by keyboard and scrolls horizontally on 360 px.

## Acceptance
- Vitest: the example validates against the `RecipeExport` schema.
- Playwright: the link in the import dialog opens the page; the page shows the sections and the example; "Kopiuj" copies the example (clipboard permission granted in the test); axe light + dark, mobile and desktop.
- `scripts/verify.sh --agent` is green.
