# F5: Recipe view, auto-match UI, scaling UI, variant editor (frontend)

**Wave 2 · depends on: F0, F1, F2 merged; reuses F4's `RecipeEditor` (coordinate via its props; if F4 isn't merged yet, build against its documented props and leave the variant-editor route to the integration at merge)**

## Goal
The cook mode: pick variants ingredient by ingredient, scale from what you have, read the steps with inline photos, create variants, and the bottom actions (deduction and shopping hooks provided by F6).

## Read first
PROJECT §4.4–4.6, §5.3, §6 (all of it) · API §4, §5 · IMPLEMENTATION_PLAN §5.2.

## Skills
`svelte-core-bestpractices`, `svelte-code-writer`, `webapp-testing`.

## Owns
`frontend/src/routes/przepisy/[id]/+page.*`, `routes/przepisy/[id]/warianty/**`, `routes/warianty/**`, `frontend/src/lib/components/recipe-view/**`, `frontend/src/lib/state/selection.svelte.ts`, `src/lib/i18n/pl/variants.ts`, `tests/integration/mocks/variants.ts`, and tests.

## Deliverables
- `SelectionState` class: loads matrix, my-selection and preferences; holds picks and scaling; derives `MatchResult` via F2 `matchVariants` (`$derived`); fetches and caches `GET /variants/{id}/resolved` for the best variant's steps; debounced (1 s) `PUT my-selection`.
- Recipe view `/przepisy/[id]` (supports `?wariant=` to pin all slots to that variant's options on first load when there is no saved selection):
  - header: title, author, a visibility icon, the recipe's **♥** (`FavoriteButton` from F4), the active variant name with its own ♥ (or **"Własna kombinacja"**), times, tags, utensils; a menu with Edytuj (only when `canEdit`; for a public recipe by the author, show "Utwórz wariant" instead), Zmień widoczność (author only, F4's `ChangeVisibilityDialog`), Eksportuj, Usuń (author only), **Nowy wariant**;
  - ingredient list from `MatchResult.ingredients`, amounts via F1 `formatIngredientAmount` with the session's liquid setting; markers for auto vs user-picked (subtle) and changed vs base;
  - tapping an ingredient opens a `BottomSheet` with two sections: **Warianty**, the ranked options (compatible first; dimmed others; supporting variant names with a small ♥ marker for favourites; the selected one checked; the last item **"+ Utwórz nowy wariant z bieżącego wyboru"**), and **Przelicz**, "Mam: [NumberField] [unit/size]" → sets scaling, with "Przywróć" to reset;
  - a scaling banner when factor ≠ 1 ("Przeliczono ×1,4 według: kurczak 600 g · Przywróć"); "Wyczyść wybór" when user pins exist;
  - Przygotowanie and Wykonanie with text and image blocks (lazy thumbnails → full on tap);
  - a sticky bottom action bar with slots for F6: **Odejmij z lodówki** and **Dodaj brakujące do listy**. Expose the current effective, scaled ingredient list via the context `CookContext` (`{ recipeId, variantId, ingredients: (Ingredient & { scaledBaseAmount })[] }`) for F6 to consume.
- Variant editor routes `/przepisy/[id]/warianty/nowy` (with an optional draft from `draftFromMatch` passed via the navigation state) and `/warianty/[id]/edytuj`, using `RecipeEditor mode="variant"`; name, description and **visibility** fields (`VisibilitySelect` from F4, with a hint when the base recipe's visibility is narrower: "Wariant zobaczą tylko osoby, które widzą przepis bazowy."); delete variant.

## Acceptance (Playwright with mocks built from `shared/test-vectors` matrices)
- Opening a recipe with 3 variants and no selection → base shown; picking an option from variant A → the other slots auto-switch to A's values and the steps change to A's.
- Pins from A and B → "Własna kombinacja"; "Utwórz nowy wariant" → the editor prefilled; save → POST body has the expected ingredients.
- Scaling: "Mam 600 g" on a 400 g anchor → every amount ×1.5 and formatted (spot-check a liquid in glasses, a spice in spoons, and onions as a size combination); the reset restores the as-entered strings.
- Reload → picks and scaling restored from the mocked `my-selection`; the PUT is debounced (one request after rapid picks).
- `?wariant=` preselects.
- Toggling ♥ on a variant updates the mocked matrix's `isFavorite` and re-ranks (a favourite variant becomes the auto choice when there are no pins or memory).
- The author of a public recipe sees "Utwórz wariant" instead of "Edytuj".
- Mobile: the sheet is reachable with one thumb and passes the axe check.

## Report

### Summary
All deliverables are in; `scripts/verify.sh --agent` is green on a branch rebased onto `main`.

- **`SelectionState`** (`src/lib/state/selection.svelte.ts`): loads recipe, matrix, my-selection and preferences (via the route `+page.ts`); `$derived` `matchVariants`, rows (slot order honours B4's `afterSlotKey`), factor, effective scaled ingredients and the steps source; fetches/caches `GET /variants/{id}/resolved`; debounced (1 s) `PUT my-selection` with `flush()` on leaving the page; `?wariant=` pins every slot when nothing is saved.
- **Recipe view** `/przepisy/[id]` (`routes/przepisy/[id]/+page.{ts,svelte}`, `components/recipe-view/`): header (visibility icon, author, times, recipe ♥, active variant name with its own ♥ or "Własna kombinacja", tags, utensils), ingredient rows with `BottomSheet` (Warianty: ranked options, supporting variant names with a ♥ marker, "+ Utwórz nowy wariant z bieżącego wyboru"; Przelicz: "Mam" + unit/size + Przywróć), scaling banner, "Wyczyść wybór", steps with lazy thumbnails that grow on tap, menu (Edytuj / Utwórz wariant or Nowy wariant / Zmień widoczność / Eksportuj / Usuń) reusing F4's `ChangeVisibilityDialog`, `DeleteRecipeDialog` and `exportRecipe`.
- **`CookContext`** (`recipe-view/cook-context.ts`, created with Svelte's `createContext`, getters so it is reactive): `{ recipeId, variantId, ingredients: (Ingredient & { scaledBaseAmount })[] }`. `CookActions.svelte` holds the sticky bar with two disabled placeholder buttons; F6 replaces them.
- **Variant editor**: `/przepisy/[id]/warianty/nowy` (seed from navigation state `page.state.variantDraft`, otherwise the base recipe) and `/warianty/[id]/edytuj` (draft rebuilt from base + resolved variant), both through `VariantForm.svelte` (name, description, `VisibilitySelect` with the narrower-base hint, F4's `RecipeEditor mode="variant"`, `toVariantRecipeWrite`); delete variant with `ConfirmDialog`.
- Tests: Vitest for slot order/rows, variant drafts and `SelectionState` (debounce, scaling, pins, favourite); Playwright `recipe-view.spec.ts` (mobile + desktop, axe on the view and the open sheet) covering every acceptance bullet. Mock fixtures in `tests/integration/mocks/variants.ts`.

### Deviations
- **Custom mix:** every `RankedOption.compatible` is false there, so the sheet does **not** dim anything in a custom mix (dimming everything would be noise); options stay ordered by `supportCount`, and the header shows "Własna kombinacja" plus a one-line hint. The steps come from the variant F2 picked.
- **Draft for "create variant from selection"** is built by my own `draftFromSelection` (`recipe-view/variant-draft.ts`), not F2's `draftFromMatch`: that returns only ids (`VariantWrite['recipe']`) and cannot seed the editor, which needs full products and "removed in this variant" markers. It follows the same rules (base ids kept for base slots, none for added ones).
- **Removed base ingredients** (pick "none" on a base slot) stay in the list, struck through ("pominięty w tym wyborze"), so the user can restore them; added slots with "none" are not shown. They are excluded from `CookContext`.
- **Archived recipes (D-27):** besides hiding Edytuj / Zmień widoczność, "Nowy wariant" and the sheet's "Utwórz nowy wariant…" are hidden too, and `/warianty/nowy` shows an explanation (conservative; PROJECT does not say whether variants of archived recipes are allowed).
- **Concurrency conflict** in the variant editor: on 409 `concurrency.conflict` the page refetches the variant's `version`, keeps the draft, shows a toast (`t.variants.editor.conflict`) and the user saves again. The recipe view itself has no versioned save (my-selection).
- **`?wariant=`** is ignored when a selection is already saved; the pins from it are not persisted until the user interacts.
- **Menu** is a `BottomSheet` with button rows (no dropdown primitive was built).
- Mock matrix is hand-built in the shape of the F2 matching vectors (variants A/B/C) with real UUIDs and complete products instead of being generated from `F2.json` (its products are partial `{id,name}`).
- `src/app.d.ts` got an additive `App.PageState.variantDraft` (SvelteKit navigation state) — the only edit outside my paths.
- Auto/"Twój wybór" markers: user pins show "Twój wybór" with a pin icon; unpinned rows that differ from the base show "Auto"; the rest have no marker.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- **F6:** replace the two placeholder buttons in `components/recipe-view/CookActions.svelte` and read the ingredients from `getCookContext()` (`recipe-view/cook-context.ts`). The page calls `setCookContext` for you.
- **B4/coordinator:** none. (`Slot.afterSlotKey` is consumed as shipped.)

### Follow-ups
- F2's `MatchResult.ingredients` still orders added slots after base rows; the view uses its own `orderSlots`. F2 could adopt `afterSlotKey` and the view could drop the duplicate.
- Option rows in the sheet show amounts scaled by the current factor; an unscaled "as entered" view of options could be considered.
- The ♥ on a supporting variant inside the sheet is display-only; toggling happens in the header for the active variant.
