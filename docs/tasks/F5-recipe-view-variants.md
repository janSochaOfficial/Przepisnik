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
