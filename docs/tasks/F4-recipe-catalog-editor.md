# F4: Recipe catalog, tabs, recipe editor, import/export UI (frontend)

**Wave 2 · depends on: F0, F1 merged**

## Goal
Browse and search recipes ("Wszystkie / Moje przepisy (Utworzone · Ulubione) / Moje warianty"), create and edit recipes with photos placed inside step text, set visibility, and enforce the direct-edit rules.

## Read first
PROJECT §3, §4 (especially §4.4–4.6), §5.2 · API §2, §3, §4, §5 (`GET /variants`), §5.3, §8 · TECHNICAL §4.

## Skills
`svelte-core-bestpractices`, `svelte-code-writer`, `webapp-testing`.

## Owns
`frontend/src/routes/przepisy/+page.*`, `routes/przepisy/nowy/**`, `routes/przepisy/[id]/edytuj/**`, `frontend/src/lib/components/recipe-editor/**` (**shared with F5's variant editor; F4 is the author, F5 reuses it**), `frontend/src/lib/components/catalog/**` (product/tag/utensil pickers), `src/lib/i18n/pl/recipes.ts`, `tests/integration/mocks/{recipes,catalog}.ts`, and tests.

## Deliverables
- **Catalog page** `/przepisy`: search (debounced), tag-filter chips, sub-tabs **Wszystkie** / **Moje przepisy** with a segmented switch **Utworzone** (`author=me`) · **Ulubione** (`GET /favorites`: recipe and variant cards mixed) / **Moje warianty** (`GET /variants?author=me`; a variant with `baseAvailable: false` shows "Przepis bazowy jest niedostępny" and only a delete action). Variant cards link to `/przepisy/[recipeId]?wariant=[variantId]`. Infinite scroll or "Pokaż więcej". Recipe cards show the cover thumbnail, title, author, times, tags, variant count, a visibility icon (lock / fridge / globe) and a **♥ toggle** (optimistic, `PUT`/`DELETE …/favorite`). Empty states.
- A reusable `FavoriteButton` component (`src/lib/components/catalog/FavoriteButton.svelte`, F4 owns it, F5 reuses it) for recipes and variants: an accessible toggle button with `aria-pressed`, labelled "Dodaj do ulubionych" / "Usuń z ulubionych".
- A reusable `VisibilitySelect` (`src/lib/components/catalog/VisibilitySelect.svelte`, F4 owns it, F5 reuses it): three options with an icon and a one-line explanation each (Prywatny / Moje lodówki / Publiczny), plus an optional `hint` prop. A FAB or header button **Nowy przepis**. An **Importuj** action (file picker → `POST /recipes/import` → navigate to the new recipe); `?import=1` opens it directly.
- **Recipe editor** component `RecipeEditor` (used by `/przepisy/nowy` and `/przepisy/[id]/edytuj`, and later by F5 in `mode="variant"`):
  - title, description, cover photo, prep and cook minutes, tags (`TagInput` with create), utensils (with create), and **visibility** (`VisibilitySelect`; only when creating a recipe; choosing Publiczny asks for confirmation with the PROJECT §4.4 text);
  - ingredients: a product `Combobox` with **inline "Dodaj produkt"** (a mini form: name, category, measure kind, display class, and the optional fields shown depending on kind), a quantity value (`NumberField`) + unit select filtered to the units compatible with the product (use F1 `toBaseAmount` ≠ null as the compatibility test), a size select for `hasSizes`, the "do smaku" toggle, note, optional; reordering (drag on desktop, up/down buttons on mobile);
  - prep and instruction steps: a block editor where each step is a list of text and image blocks, **"Wstaw zdjęcie"** between paragraphs (splits the text block at the cursor), upload with progress, caption, delete and reorder;
  - `mode="variant"` props: base rows cannot be reordered (PROJECT §6.2). Their delete action becomes "Usuń w wariancie", which marks the row as removed. New rows can be inserted anywhere. Title, description, cover and tags are hidden.
  - Client validation mirroring API limits. Server `errors` paths are mapped back onto fields.
  - Unsaved-changes guard (`beforeNavigate`).
- **Direct-edit rules (PROJECT §4.5):** `/przepisy/[id]/edytuj` on a recipe with `canEdit: false` because it's public shows an explanation and a "Utwórz wariant" button instead of the form. On save, a 409 `recipe.has_variants` opens a `ConfirmDialog` with the counts from the response ("Zapisanie zmian usunie wszystkie warianty tego przepisu (N, w tym M innych osób). Kontynuować?"). Confirming retries with `deleteVariants: true`, and cancelling keeps the unsaved form. Show the same warning up front as a banner when `totalVariantCount > 0`.
- **Change visibility** dialog (`src/lib/components/catalog/ChangeVisibilityDialog.svelte`, opened from F5's recipe menu): `PUT /recipes/{id}/visibility`, the public confirmation, and a 409 `recipe.visibility_locked` explained ("Nie można zawęzić widoczności — inni użytkownicy mają warianty tego przepisu.").
- Delete recipe (with a confirm that mentions archiving when `variantCount` has foreign variants; the UI just says "Przepis zostanie ukryty, bo inni mają jego warianty" when the API archived it).
- An **Eksportuj** action on the recipe (the menu lives in F5's view; F4 provides `exportRecipe(id)` in `src/lib/components/recipe-editor/transfer.ts`).

## Acceptance (Playwright with mocks)
- Create a recipe with 3 ingredients (one via inline new product), 2 instruction steps, one photo inserted mid-text → the POST body matches the contract (assert the shape against `schema.d.ts` types plus a JSON snapshot).
- Editing keeps ingredient ids (asserted in the PUT body).
- An incompatible unit is not offered; a server 400 `quantity.unit_incompatible` lands on the right row.
- Import a sample file (`shared/samples/export-v1.json`, or a local fixture if B6 isn't merged) → navigates to the new recipe.
- Moje warianty lists variants and links with `?wariant=`. Ulubione shows recipe and variant cards. Toggling ♥ off in Ulubione removes the card, with an undo toast.
- Editing a private recipe with variants → 409 → confirm → the second PUT has `deleteVariants: true`; cancel → no second request, and the form stays dirty.
- Public recipe → the edit page shows "Utwórz wariant" instead of the form.
- Narrowing visibility with a 409 → the explanation is shown.
- Component tests: block-editor split/insert/remove; unit select filtering.

## Report

### Summary
All deliverables are in; `scripts/verify.sh --agent` is green on a branch rebased onto `main` (993721d).

- **Catalog** `/przepisy` (`routes/przepisy/+page.svelte`, `catalog/catalog.svelte.ts`): tabs Wszystkie / Moje przepisy (Utworzone · Ulubione) / Moje warianty, state kept in the URL (`?tab=&sub=&q=&tags=`), debounced search, tag chips, "Pokaż więcej", skeletons, error block, empty states. `RecipeCard` and `VariantCard` (variant links use `?wariant=`; `baseAvailable: false` shows the note and only a delete action). Header actions: Importuj, Nowy przepis.
- **Reusable catalog components** in `components/catalog/`: `FavoriteButton` (optimistic, `aria-pressed`), `VisibilitySelect` (3 options with icon + hint, optional `hint`, built-in "Publiczny" confirmation, `confirmPublic={false}` to opt out), `ChangeVisibilityDialog` (props `open`, `recipeId`, `visibility`, `onChanged`; explains 409 `recipe.visibility_locked`), `ProductPicker` + `NewProductDialog` (inline "Dodaj produkt"), `names.ts` (tag/utensil name to id resolution).
- **Recipe editor** in `components/recipe-editor/`: `RecipeEditor` (`mode="recipe" | "variant"`), `IngredientList`/`IngredientRow`, `StepList` (block editor), `ImageUploadButton` (XHR upload with progress), `ImportDialog`, `DeleteRecipeDialog`, `transfer.ts` (`exportRecipe(id)`, `importRecipe(file)`), pure modules `draft.ts` (draft model, `toRecipeWrite`, `toVariantRecipeWrite`, `draftFromRecipe(r, {base})`, `validateDraft`, `mapServerErrors`, `compatibleUnits`), `blocks.ts`, `lists.ts`.
- **Routes**: `/przepisy/nowy`, `/przepisy/[id]/edytuj` (+ `+page.ts`), with the PROJECT §4.5 rules: public/archived/not-author shows an explanation ("Utwórz wariant" for public), banner when `totalVariantCount > 0`, 409 `recipe.has_variants` → `ConfirmDialog` → retry with `deleteVariants: true`, cancel keeps the dirty form.
- Unsaved-changes guard with `beforeNavigate` (in-app navigation asks via `ConfirmDialog`; closing the tab uses the browser prompt).
- Tests: Vitest for `blocks`, `lists`, `draft`, `transfer/upload`, plus component tests (`StepList` split/insert/remove/variant mode, `IngredientRow` unit filtering). Playwright (`recipe-editor.spec.ts`, `recipe-catalog.spec.ts`, mobile and desktop, axe in light and dark on the catalog and the editor) covers every acceptance bullet. Mock fixtures in `tests/integration/mocks/{recipes,catalog}.ts`.

For F5 (variant editor): `draftFromRecipe(resolvedRecipe, { base: true })` marks all rows as base rows; `<RecipeEditor mode="variant" bind:draft … />` locks base order, turns delete into "Usuń w wariancie" (row stays, `removed: true`, restorable), allows new rows anywhere ("Wstaw składnik poniżej") and hides title, description, cover and tags. Convert with `toVariantRecipeWrite(draft, utensilIds)` (it also returns `maps` for `mapServerErrors(error.errors, maps, 'variant')`). The page owns the request and passes `error`, `serverErrors`, `saving`, `submitLabel` and `guard`.

### Deviations
- **Inline new product** has no `pieceForms` fields (the other optional fields are shown per measure kind). Not needed for the acceptance flow; the API accepts a product without them.
- **Search box and tag chips** are shown only for the views backed by `GET /recipes` (Wszystkie, Utworzone); `GET /favorites` and `GET /variants` have no such filters in the contract.
- **Import** opens a dialog with a file field (`?import=1` opens the dialog), because browsers refuse to open a file picker without a user click.
- **"JSON snapshot"** of the create body is asserted as an inline expected object typed with `satisfies Schemas['RecipeCreate']` (so contract drift fails the compile) instead of snapshot files, which would be per-platform and per-project.
- The edit page also hosts **"Zmień widoczność"** and **"Usuń przepis"** (author only, not archived), so the dialogs have a place in F4 and are testable before F5's menu exists. F5 can reuse `ChangeVisibilityDialog` and `DeleteRecipeDialog` from its menu.
- `shared/samples/export-v1.json` does not exist yet (B6), so the import test uses a local fixture (`sampleExport` in `mocks/recipes.ts`).
- Two client-side texts are `fieldErrors` keys of my own (`productRequired`, `quantityRequired`, …) next to the API codes; server codes `quantity.unit_incompatible`, `quantity.invalid`, `image.not_owned` and the short codes `required`, `too_long`, `out_of_range` are mapped onto the right row (block-level errors collapse onto their step).
- No dropdown-menu primitive was needed, so none was built.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- **F5:** open `ChangeVisibilityDialog` and `DeleteRecipeDialog` from the recipe menu, call `exportRecipe(id)` for "Eksportuj", and link "Utwórz wariant" to `/przepisy/[id]/warianty/nowy` (F4 already links there from the read-only edit page). Use `FavoriteButton` (`kind="variant"`) in the variant picker.
- **F3:** the profile's "Importuj przepis" should link to `/przepisy?import=1`.

### Follow-ups
- After "Zmień widoczność" on the edit page the loaded recipe is refreshed (`invalidate`), but the form draft keeps what the user already typed (it is seeded once).
- On create, tags and utensils are always sent through `POST /tags` / `POST /utensils` (the server returns the existing entry); a lookup by the loaded suggestions could save a few requests.
- Drag and drop of ingredient rows (desktop) is implemented with native HTML5 drag events and is covered only by the pure `dropRow` unit tests, not by Playwright.
