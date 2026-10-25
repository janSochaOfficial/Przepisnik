# R1: Recipe view: Warianty tab, opening a variant, the recipe ♥ by the title (frontend)

**Wave v1.2 · depends on: nothing · slot 1 · model `sonnet`, effort `medium` (a real bug in selection state plus a page restructure; no backend)**

## Goal
PROJECT §3, §4.6, §6.7 (D-38, D-39):
1. **Bug (fix first, starting with a failing test).** Opening a variant (a variant card in Ulubione / Moje warianty, the `?wariant=` link after an import) shows the base recipe's ingredients instead of the variant's, and scaling then applies to the wrong amounts.
2. A **Warianty (N)** tab on the recipe view.
3. The recipe's ♥ moves next to the title, so it is not confused with the active variant's ♥.

## Read first
PROJECT §3, §4.6, §5.3, §6 (all of it, especially the new §6.7) · API §5 (`GET /recipes/{id}/variants`, `VariantSummary`, the matrix, `my-selection`) · `frontend/DESIGN.md` · `src/lib/state/selection.svelte.ts` (+ its test) · `src/routes/przepisy/[id]/+page.svelte` / `+page.ts`.

## Skills
`svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`), `webapp-testing` to look at the result.

## Likely causes of the bug (confirm, don't assume)
- `SelectionState`'s constructor uses `pinVariantId` only when **no** selection is remembered (`if (init.selection) … else if (init.pinVariantId)`), so anyone who has opened the recipe before gets their remembered picks (often the base) instead of the variant.
- The page seeds `SelectionState` once (`svelte-ignore state_referenced_locally`); navigating to the same route with another `?wariant=` reuses the component and never re-seeds.
- The remembered scaling anchor may point at a slot whose option the variant changes or removes.

## Owns
`src/routes/przepisy/[id]/+page.svelte`, `+page.ts`, `src/lib/state/selection.svelte.ts` (+ tests), `src/lib/components/recipe-view/**` (new components here, e.g. `VariantList.svelte`), the recipe-view keys in `i18n/pl/variants.ts`, recipe-view mocks and specs in `tests/integration/`.

## Touch only if needed
`components/catalog/FavoriteButton.svelte` / `VariantCard.svelte` (reuse; no behaviour change for other screens) · `components/shell/Page.svelte` / `PageHeader.svelte`: an **additive** optional snippet next to the title (e.g. `titleAside`), with every existing page unchanged · `components/ui/Tabs.svelte`: reuse as is.

## Deliverables
- **Opening a variant** (D-38): `?wariant=<id>` pins every slot to that variant's option, whatever is remembered, and the remembered picks are replaced (saved through the normal debounced `my-selection` write). The scaling choice is kept if its anchor slot still has an amount, otherwise reset. `?wariant=` for an unknown / invisible id falls back to the remembered state, without an error. Changing `?wariant=` on the same page (client navigation) re-applies this without a reload; `+page.ts` and the page must react to the URL, not only the first load.
- **Warianty (N) tab**: `Tabs` with **Przepis** (today's view) and **Warianty (N)**, N = visible variants. Entries: "Przepis bazowy" first, then variants (most cooked, then oldest), each with name, @author, visibility label, cook count, its ♥ and a "wyświetlany" mark on the one shown (the best variant; none for a custom mix). Data: the matrix already has the variants, use `GET /recipes/{id}/variants` only if a field is missing. Tapping an entry navigates to `?wariant=<id>` (base: clears the param and the picks) and switches to Przepis. The selected tab is not in the URL. Empty state when there are no variants, with the existing "create a variant" action if allowed.
- **♥ placement** (D-39): the recipe's `FavoriteButton` sits next to the page title (header). The active variant's ♥ stays next to the active variant name. Both keep distinct accessible names ("Ulubiony przepis" / "Ulubiony wariant", or the existing keys).
- Polish strings only in `i18n/pl/`.

## Acceptance
- Failing-first tests: a Vitest test of `SelectionState` with a remembered base selection **and** `pinVariantId` → the variant's options are pinned; a Playwright spec: open `/przepisy/R?wariant=V` with a remembered selection → the ingredient list shows V's products and amounts; scale with "Mam:" → V's amounts are scaled.
- Playwright: open another variant from the Warianty tab → the view switches to Przepis and shows that variant; back to "Przepis bazowy" → base ingredients; the recipe ♥ is in the header and toggles the recipe favourite only; axe (light + dark) on both tabs, mobile and desktop.
- No regressions in the existing recipe-view, favourites and import-variants specs.
- `scripts/verify.sh --agent` is green.

## Report

### Summary
Fixed the variant-opening bug and added the Warianty (N) tab and the recipe heart by the title (branch `task/R1-recipe-view-variants`).
- Root cause: `SelectionState` only used `pinVariantId` when no selection was remembered, and the page seeded the state once. Now `SelectionState.openVariant(id)` pins every slot to that variant, replaces the remembered picks (debounced `my-selection` save) and keeps the scaling only if its anchor slot still has an amount. The constructor calls it for `?wariant=`; unknown ids change nothing. The page re-applies a changed `?wariant=` (links, back/forward) without a reload.
- New `components/recipe-view/VariantList.svelte` (base first, then most cooked, then oldest; name, @author, visibility, cook count, variant heart, "wyświetlany" mark; empty state with "Utwórz wariant"). Page uses `Tabs` (Przepis / Warianty (N)); the tab is not in the URL. Opening an entry navigates to `?wariant=<id>` (base: no param) and switches to Przepis.
- Recipe heart moved to the header via an additive `titleAside` snippet on `Page` / `PageHeader`. `FavoriteButton` got optional `addLabel` / `removeLabel` (defaults unchanged); names are now "Dodaj przepis do ulubionych", "Dodaj aktywny wariant do ulubionych", and per-variant names in the tab.
- Tests: Vitest for `SelectionState` (written first, failing), Playwright specs for the remembered-selection bug, the tab, navigation, heart placement, empty state and axe (light and dark, both viewports). Mock `GET /recipes/:id/variants` added. `scripts/verify.sh --agent` green after rebasing onto main.

### Deviations
- The base entry of the tab pins every slot to the base (user picks) instead of clearing the picks. Clearing would let a favourite variant win the auto-match and show it instead of the base. The "Wyczyść wybór" button therefore shows after choosing the base.
- Visibility is not in the matrix, so the tab fetches `GET /recipes/{id}/variants` lazily; the visibility label appears when it arrives.
- The base entry has no heart (the recipe heart is in the header, D-39).
- The old test "does not use ?wariant= when a selection is saved" contradicted D-38 and was replaced.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
None.

### Follow-ups
- The opening of a variant by `?wariant=` triggers a `my-selection` PUT even when the picks are identical to the remembered ones (harmless).
