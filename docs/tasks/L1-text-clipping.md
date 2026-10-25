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
