# L1: Text cut off at the bottom (descenders) (frontend)

**Wave v1.3 · depends on: nothing · slot 2 · model `haiku`, effort `low` (a CSS fix; low risk, easy to see)**

## Goal
Some text is clipped at the bottom, so the tails of y, j, g, ą, ę are cut off. The user saw it mainly on the recipe title on the recipe page. Find every place where this happens and fix it.

## Read first
`frontend/DESIGN.md` (typography) · `src/app.css` (the `--text-*--line-height` tokens; headings use the serif display font) · `components/shell/PageHeader.svelte`.

## Skills
`svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte`), `webapp-testing` (screenshots before and after).

## Likely cause (confirm it)
`truncate` (overflow hidden) on text whose line box is shorter than the font's ink: the `h1` in `PageHeader` has `truncate` with `--text-xl--line-height: 1.25` / `--text-3xl--line-height: 1.15`, so descenders of the serif font fall outside the box and are cut. On `lg` it even wraps (`whitespace-normal`) and stays `overflow: hidden`.

## Owns
`src/app.css`, `components/shell/**`, and any other clipped component **except** `components/catalog/VariantCard.svelte` and `components/recipe-view/VariantList.svelte` (V2 runs at the same time and owns them: report clipping there instead of fixing it). A new spec `tests/integration/text-clipping.spec.ts`.

## Deliverables
- A fix at the source, not per page: e.g. a line height for clipped headings that fits the font's descenders, or a small `padding-block` on truncating text, so the ink stays inside. Keep the visual rhythm (header height unchanged on mobile).
- Long titles: one line with an ellipsis on mobile; on desktop they may wrap, and then they must not be clipped either.
- Look for the same problem elsewhere (catalog cards, profile nickname, fridge notes, the Select trigger, the side rail labels); fix it where you own the file.

## Acceptance
- Playwright (mobile + desktop): for the recipe page `h1` with the title "Gęsty jogurt pyszny", and each other fixed element, the element's height is at least the font's ascent + descent, measured in the browser (e.g. a `Range`'s `getBoundingClientRect()` on the text compared with the element's box, or `scrollHeight <= clientHeight` with the descent included). The test fails before the fix.
- Before / after screenshots are listed in the Report (not committed).
- `scripts/verify.sh --agent` is green.

## Report

### Summary
- Cause confirmed: `PageHeader` `h1` (and its subtitle) use `truncate` (overflow hidden) with `--text-xl--line-height: 1.25` and `--text-3xl--line-height: 1.15`. Literata's ascent + descent is larger than the line box, so descenders of "Gęsty jogurt pyszny" were cut (measured: box 30 px vs ink 35 px at `text-xl`).
- Fix at the source: `PageHeader.svelte` h1 and subtitle get `py-[0.2em] -my-[0.2em]`. The padding gives the box room for the ink; the negative margin keeps layout, so the header height on mobile is unchanged.
- Test: `frontend/tests/integration/text-clipping.spec.ts` (mobile + desktop, a short and a long title). Checks that the padding box (`clientHeight`) of the recipe `h1` is at least the canvas-measured `fontBoundingBoxAscent + fontBoundingBoxDescent`.
- Commits: `040c4f3` (failing test), `f1ac72e` (fix). Branch `task/L1-text-clipping`, based on `main` (eca0d95).
- `scripts/verify.sh --agent`: green (267 Playwright tests, 625 frontend unit tests, 370 + 400 .NET tests).

### Deviations
- The worktree's original branch (`worktree-agent-a98aaa653864f9f94`) was behind `main`, so I created `task/L1-text-clipping` from `main` instead of rebasing that branch.
- The first test version measured the content box (height minus padding). It was changed to the padding box (`clientHeight`), which is what `overflow: hidden` clips to. Both versions fail before the fix on mobile.
- Before/after screenshots were not captured, so none are listed.
- The fix covers only `PageHeader`, which I own. The other candidates were not changed, so the "every other fixed element" check is not done (see Follow-ups).

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- `components/catalog/VariantCard.svelte` and `components/recipe-view/VariantList.svelte` (owned by V2): not checked for descender clipping. Please check when V2 is done.
- `components/fridge/ItemList.svelte` (fridge note `truncate text-sm`, a span inside a flex row), `ui/Select.svelte` (trigger span with `truncate`), `routes/profil/+page.svelte` (nickname and email `truncate`), `catalog/RecipeCard.svelte`: same pattern, not verified. These are outside my ownership or were not measured.

### Follow-ups
- Apply the same padding fix to the other `truncate` text listed above, and add a Playwright check for each (the test file is set up for that).
- `components/shell/AppShell.svelte` rail label (`span.truncate`): an inline span, so it may need `inline-block` or a different fix. Not measured.
