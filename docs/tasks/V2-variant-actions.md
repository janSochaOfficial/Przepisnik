# V2: ⋯ menu on variants: edit, change visibility, delete (full stack)

**Wave v1.3 · depends on: nothing · slot 1 · model `sonnet`, effort `medium` (one small endpoint plus a reused menu in three places; the delete flow has state to refresh)**

## Goal
The variant author can edit, change the visibility of, and delete a variant from the variant lists (PROJECT §6.5, D-41). Today this is only possible inside `/warianty/{id}/edytuj`, which nothing links to.

## Read first
PROJECT §4.4, §6.1, §6.5, §6.7 · API §5 (variants) · `frontend/DESIGN.md` · `components/recipe-view/VariantList.svelte`, `components/catalog/VariantCard.svelte`, `components/catalog/ChangeVisibilityDialog.svelte`, the recipe menu in `routes/przepisy/[id]/+page.svelte` (a `BottomSheet`), `routes/warianty/[id]/edytuj/+page.svelte`, the variant delete in `routes/przepisy/+page.svelte`.

## Skills
`dotnet-webapi`, `svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Contract (API.md §5 + openapi.yaml together)
| Method | Path | Body | Response | Errors |
|---|---|---|---|---|
| PUT | `/variants/{id}/visibility` | `{ visibility: Visibility }` | `200 VariantDetail` | 403 `variant.not_author`, 404 `variant.not_found` |

No lock: a variant's visibility can always change (PROJECT §6.1). It bumps the variant's version like any update.

## Owns
`Core/Variants/VariantService.cs` (the visibility method only), `Api/Features/Variants/**` (the endpoint), their tests, `backend/http/variants.http`; frontend: a new `components/catalog/VariantActions.svelte` (the ⋯ `BottomSheet` + dialogs), `VariantCard.svelte`, `recipe-view/VariantList.svelte`, the variant keys in `i18n/pl/variants.ts` / `recipes.ts`, variant mocks and specs.

## Touch only if needed
`ChangeVisibilityDialog.svelte`: generalise it (`kind: 'recipe' | 'variant'`, recipe behaviour unchanged) rather than copying it · `routes/przepisy/+page.svelte` and `routes/przepisy/[id]/+page.svelte`: wiring only (refresh after a change).

## Deliverables
- **`VariantActions`** (⋯ icon button, accessible name "Akcje wariantu „X”"), shown only when the viewer is the variant's author:
  - **Edytuj wariant** → `/warianty/{id}/edytuj`. Hidden when the base recipe is unavailable (only delete is possible then, §6.5).
  - **Zmień widoczność** → the visibility dialog, saved with the new endpoint; the card / list entry updates in place.
  - **Usuń wariant** → a confirmation "Usunąć wariant „X”?" (destructive button), then `DELETE /variants/{id}`, a toast, and the entry disappears.
- **Where:** each variant entry in the Warianty tab, and `VariantCard` (Moje warianty and Ulubione). On a card whose base is unavailable, the menu replaces today's trash button (delete only).
- **Recipe page after a delete:** the matrix is reloaded (`invalidate('app:recipe:<id>')` or equivalent), the Warianty count drops, and if the deleted variant was the one shown, the page goes to the base (`?wariant=` dropped). After a visibility change the entry's label updates.
- The ⋯ button and the ♥ sit side by side without overlapping the card's full-card link (`relative z-10`, as the ♥ does), with touch targets ≥ 44 px.

## Acceptance
- Integration tests: visibility by the author → 200 with the new visibility and a new version; by another user → 403; unknown / invisible → 404; invalid value → 400.
- Playwright: as the author, from the Warianty tab: change visibility (label updates), delete the shown variant (confirmation, toast, base shown, count −1), edit (lands on the editor). From Moje warianty: delete and change visibility. Another user's variant has no menu. Axe light + dark on the open menu and the confirmation, mobile and desktop.
- `scripts/verify.sh --agent` is green.

## Report

### Summary
- Backend: `PUT /variants/{id}/visibility` (`IVariantService.SetVisibilityAsync`, `VariantsFeature`, `VariantVisibilityRequest`), author only, never locked, bumps `UpdatedAt` and the row version; returns `VariantDetail`. Integration tests: author 200 with new visibility and new version, other viewer 403, invisible / unknown 404, invalid or missing value 400. `backend/http/variants.http` has the request.
- Frontend: new `components/catalog/VariantActions.svelte` (⋯ `BottomSheet` with Edytuj wariant / Zmień widoczność / Usuń wariant, the visibility dialog and a confirmation "Usunąć wariant „X”?", accessible name "Akcje wariantu „X”", 44 px target, `relative z-10`). Shown only to the author (`page.data.me.id`). Used in `VariantCard` (replaces the trash button; delete only when the base is unavailable) and in each `VariantList` entry.
- `ChangeVisibilityDialog` generalised with `kind: 'recipe' | 'variant'`; its `recipeId` prop became `id` (two recipe call sites updated, behaviour unchanged).
- Recipe page: `SelectionState.reloadMatrix()` refetches the matrix after a delete (drops stale picks, updates `variantCount`); if the deleted variant was shown (or pinned by `?wariant=`) the page opens the base and drops `?wariant=`. A visibility change updates the entry's label from the list's local visibility map.
- `przepisy/+page.svelte`: the old page-level delete confirmation was removed (the card now deletes itself and reports `ondeleted`).
- Playwright `variant-actions.spec.ts` (mobile + desktop): visibility change, delete of the shown variant (confirmation, toast, base shown, count -1, URL), delete of another variant, edit link, foreign variant without menu, 44 px target, axe light/dark on the open menu and the confirmation, Moje warianty delete / visibility, base-unavailable card with delete only, foreign favourite without menu. `recipe-catalog.spec.ts` adapted to the new menu.

### Deviations
- After a delete the recipe page calls `selection.reloadMatrix()` instead of `invalidate('app:recipe:<id>')`, because `SelectionState` is seeded once from the load data and an invalidate would not refresh it (the spec allows "or equivalent").
- i18n: the old `recipes.catalog.deleteVariant*` / `variantDeleted` keys moved to `variants.variantMenu` (the key `variants.actions` was already taken by the cook actions).
- Choosing "Publiczny" in the dialog still asks for the usual confirmation from `VisibilitySelect`.
- The visibility item is disabled until the list's visibility map has loaded (the matrix carries no visibility).

### Model changes
None.

### Contract changes
API.md §5 table and openapi.yaml: `PUT /variants/{id}/visibility` (`SetVariantVisibility`, request `VisibilityWrite`, response `VariantDetail`). `schema.d.ts` regenerated.

### Dependencies added
None.

### Requests for other tasks
None.

### Follow-ups
- The Warianty tab fetches visibility from `GET /recipes/{id}/variants`; adding `visibility` to the matrix variants would remove that extra request.
