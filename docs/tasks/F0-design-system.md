# F0: Design system, UI primitives, app shell

**Wave 1 · depends on: T00**

## Goal
Choose the visual direction once, and build the primitives and shell that every UI task reuses, so that F3–F6 never restyle.

## Read first
PROJECT §1, §3, §11 · TECHNICAL §4.

## Skills
`frontend-design` (for the design plan), `svelte-core-bestpractices`, `svelte-code-writer` (run the autofixer on every component), `webapp-testing` (screenshots for self-critique).

## Owns
`frontend/DESIGN.md`, `frontend/src/app.css`, `frontend/src/lib/components/ui/**`, `frontend/src/lib/components/shell/**`, `frontend/src/routes/+layout.svelte`, `frontend/src/lib/i18n/pl/common.ts`, a dev-only showcase route `frontend/src/routes/(dev)/ui/+page.svelte`, and tests for these.

## Deliverables
- `DESIGN.md`: the design plan following the skill (palette of 4–6 named colours for light and dark, type scale, spacing, radius, layout concept for 360 px and desktop, principles). The subject is a home kitchen notebook for Polish cooks, used on a phone at the counter. Record what was rejected as too generic.
- Tokens as CSS custom properties, mapped into the Tailwind 4 theme. Light and dark modes.
- Primitives (bits-ui based where applicable), all keyboard and screen-reader friendly, ≥ 44 px touch targets:
  `Button` (primary/secondary/ghost/danger, loading), `IconButton`, `TextField`, `NumberField` (Polish decimal comma input!), `TextArea`, `Select`, `Combobox` (async search + "create new" footer item), `Chip`/`TagInput`, `Switch`, `Slider` (step 5), `Dialog`, `BottomSheet` (sheet on < 768 px, popover on desktop, one component), `Toast` service with an optional action (for undo), `EmptyState`, `Skeleton`, `ErrorMessage` (takes an `ApiError` and shows `t.errors[code]`), `ConfirmDialog`, `Tabs`, `Avatar`.
- App shell: a bottom tab bar on mobile and a side rail at ≥ 1024 px with the four destinations (PROJECT §3), a page header with back and actions slots, safe-area insets.
- The `(dev)/ui` showcase page renders every primitive in every state. It is excluded from production builds or guarded by `dev`.

## Acceptance
- Component tests for `NumberField` (`"1,5"` → 1.5, rejects `"abc"`), `Combobox` (debounced search, create-new item), `BottomSheet` (focus trap, Escape closes), `Toast` (action callback, auto-dismiss).
- A Playwright integration test of the showcase at mobile and desktop with no a11y violations (`@axe-core/playwright`).
- Screenshots of the showcase in light and dark at 360 px and 1280 px, attached in the Report (paths).
