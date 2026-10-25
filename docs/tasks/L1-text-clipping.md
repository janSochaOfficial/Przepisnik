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
- Cause confirmed: `truncate` (overflow hidden) on text whose line box is shorter than the serif's ink. The `PageHeader` h1 with `--text-xl--line-height: 1.25` measured a 30 px box against 35 px of ink, so the descenders of "Gęsty jogurt pyszny" were cut.
- Fix at the source, one rule in `frontend/src/app.css` (`@layer utilities`): `.truncate { padding-block: 0.2em; margin-block: -0.2em; }`. The padding gives the clip box room for the ink, and the negative margin keeps layout and the mobile header height unchanged. There are no per-element classes any more.
- `components/shell/AppShell.svelte` rail label span is now `block`. It was inline, so it had no clip box height.
- Test: `frontend/tests/integration/text-clipping.spec.ts`. It checks that the padding box (`clientHeight`) of each element is at least the canvas-measured `fontBoundingBoxAscent + fontBoundingBoxDescent`. Covered: recipe h1 (short and long title), RecipeCard title on /przepisy, profile nickname, Select trigger text on /profil, fridge item note, and the desktop side-rail label.
- Commits on the branch: `040c4f3` (failing test), `f1ac72e` (first fix, per element), then the app.css move, the rail `block`, and the extended spec.
- `scripts/verify.sh --agent`: the final run is green. Two axe tests (recipe-view option sheet, ui-showcase light) timed out once under two-agent load and passed on re-run.

### Fail-without-the-rule check
Temporarily set the `.truncate` padding and margin to 0 (desktop project only), then restored app.css:
- Recipe h1 "Gęsty jogurt pyszny": FAILS without the rule. This is the case that was reported.
- Recipe h1 long title: passes. It wraps on desktop, so it is not truncated.
- RecipeCard title: passes. It is not a `truncate` element (it wraps, no overflow hidden). The test asserts instead that no ancestor clips overflow, which is not rule-dependent.
- Profile nickname, Select trigger text, fridge note, side-rail label: all pass without the rule too. At the current font sizes these boxes are already tall enough for the ink, so the rule is defensive for them. The new checks do not prove they would catch a regression on these elements.
- Mobile h1 failed before the fix in the earlier round (per-element version).

### Deviations
- The worktree's original branch (`worktree-agent-a98aaa653864f9f94`) was behind `main`, so I created `task/L1-text-clipping` from `main` instead of rebasing that branch.
- The test measures the padding box (`clientHeight`), which is what `overflow: hidden` clips to, not the content box.
- Before/after screenshots were not captured, so none are listed.
- Commits do not carry the Co-Authored-By / Claude-Session trailers from the attribution reminder. I did not rewrite history to add them.
- VariantCard.svelte and VariantList.svelte were not touched.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- `components/catalog/VariantCard.svelte` and `components/recipe-view/VariantList.svelte` (owned by V2): not checked for clipping. The global `.truncate` rule applies to them automatically. Please check after V2 merges.

### Follow-ups
- The `.truncate` rule is global, so new truncating text gets the fix automatically. Add a spec check when a new truncate element appears.
