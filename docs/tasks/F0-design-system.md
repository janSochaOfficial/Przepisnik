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

## Report

### Summary
- **Direction "Bolesławiec"** (cool stoneware grey, cobalt as the only strong colour, dill and beet as small secondaries, the *oczko* dot-and-ring motif as the single signature), recorded in `frontend/DESIGN.md` with the palette (light and dark, contrast checked), type scale, spacing, radius, elevation, layout for 360 px and desktop, motion, wording, a per-component usage guide and a checklist for new screens, plus what was rejected as too generic.
- **Tokens** in `src/app.css` as `--rl-*` custom properties mapped into the Tailwind 4 theme (`bg-paper`, `text-ink`, `bg-cobalt`, `rounded-control/card/sheet`, `shadow-raised/overlay`, own type scale). Light/dark via `prefers-color-scheme`; `prefers-reduced-motion` disables all motion. Fonts: Literata (titles) and Atkinson Hyperlegible Next (everything else), self-hosted.
- **Primitives** in `src/lib/components/ui` (barrel `index.ts`): `Button`, `IconButton`, `TextField`, `NumberField` (+ `parseDecimal`/`formatDecimal`), `TextArea`, `Select`, `Combobox`, `Chip`, `TagInput`, `Switch`, `Slider`, `Dialog`, `BottomSheet`, `toast` + `Toaster`, `EmptyState`, `Skeleton`, `ErrorMessage`, `ConfirmDialog`, `Tabs`, `Avatar`, plus `Field`, `Spinner`, `Oczko`. bits-ui is used for Select, Combobox, Switch, Slider, Dialog, AlertDialog, Popover, Tabs, Avatar.
- **Shell** in `src/lib/components/shell`: `AppShell` (one `<nav>`: bottom tab bar < 1024 px, side rail ≥ 1024 px, skip link, safe areas, hidden on `/logowanie` and `/witaj`), `Page` (gutters, width, header), `PageHeader` (back + h1 + actions), `ActionBar` (sticky main actions). `routes/+layout.svelte` mounts `AppShell` and `Toaster`.
- **Showcase** `routes/(dev)/ui` renders every primitive in every state; guarded by `dev || VITE_UI_SHOWCASE === '1'` (404 otherwise, unit-tested).
- **Tests:** component tests for NumberField (`"1,5"` → 1.5, rejects `"abc"`, min, outside updates), Combobox (debounce, abort, pick, create-new item), BottomSheet (focus trap, Escape, focus return; sheet on mobile, popover on desktop), Toast (action callback, auto-dismiss, pause, max 3, live regions); unit tests for decimal parsing and nav mapping. Playwright `tests/integration/ui-showcase.spec.ts`: axe (WCAG 2.1 AA) on the showcase and with sheet, dialog and toast open, light and dark, mobile (Pixel 7) and desktop; tab bar vs rail geometry and 44 px targets; sheet vs popover; decimal comma.
- **Screenshots** (`docs/tasks/F0-screenshots/`): `showcase-360-light.png`, `showcase-360-dark.png`, `showcase-1280-light.png`, `showcase-1280-dark.png` (full page; the fixed tab bar and action bar appear mid-image because of full-page capture), plus `showcase-360-light-sheet.png`, `showcase-1280-dark-sheet.png`, `showcase-360-dark-dialog.png`, `showcase-1280-light-combobox.png`, `showcase-360-light-toast.png`.
- `scripts/verify.sh --agent` is green after rebasing onto `main` (868ed46).

### Deviations
- **`frontend/playwright.config.ts` edited (not owned):** one line adds `VITE_UI_SHOWCASE: '1'` to the `webServer` env, so the integration build contains the showcase that the acceptance a11y test needs. A plain `dev` guard would make the showcase 404 in the (production) Playwright build. Normal production builds still answer 404.
- **Screenshots location:** the task does not say where to store them; they are in `docs/tasks/F0-screenshots/` next to this file (not an owned path).
- **Dependencies:** two font packages added (below). System fonts would have been the conservative choice, but the design skill asks for deliberate typefaces, and self-hosting keeps the app free of external CDNs.
- **Toast** is a module singleton (`toast` in `toast.svelte.ts`) rather than a `createContext` class, so it can be called from load functions and plain modules. Safe because the app is a client-only SPA (TECHNICAL §4.1). Tests create their own `ToastQueue`.
- **`BottomSheet` requires a `trigger`** (the popover needs an anchor on desktop).
- `common.ts` contains a `showcase` subtree with the texts of the dev-only page (TECHNICAL forbids Polish literals outside `i18n/pl/`). All existing keys are unchanged (additive only).
- Kept deprecated Tailwind aliases `text`, `accent`, `border` → `ink`, `cobalt`, `line` so the T00 placeholder pages (`/logowanie` uses `border-border`, `bg-accent`) keep working. They can be removed once F3 rebuilds those pages.
- Component tests query floating bits-ui content with `hidden: true` (floating-ui keeps it `visibility: hidden` in jsdom). `ui/testing.ts` adds the jsdom polyfills bits-ui needs (ResizeObserver, pointer capture, `matchMedia` with `setViewport()`, `getClientRects` for the focus trap); feature component tests that render these primitives should import it.

### Model changes
None.

### Contract changes
None.

### Dependencies added
- `@fontsource-variable/literata@5.3.0` (dev) and `@fontsource-variable/atkinson-hyperlegible-next@5.3.0` (dev), commit `deps: add @fontsource-variable/literata and atkinson-hyperlegible-next for F0`.

### Requests for other tasks
- **Coordinator:** accept the one-line `playwright.config.ts` change (above).
- **F3–F6:** start every screen with `<Page>` from `$lib/components/shell` and follow `frontend/DESIGN.md` §10–12. `PagePlaceholder.svelte` (T00) does not use `Page`, so placeholder pages now render without gutters; F3–F6 replace them anyway (or the coordinator can wrap `PagePlaceholder` in `Page`).
- **F3:** rebuild `/logowanie` and `/witaj` with the primitives; then the deprecated colour aliases in `app.css` can be dropped.

### Follow-ups
- Toasts on bare routes (`/logowanie`, `/witaj`) still keep the tab-bar offset; harmless, could be zeroed if those pages use toasts.
- No dropdown-menu primitive yet (`PageHeader` suggests a "Więcej" menu for more than two actions); add one from bits-ui `DropdownMenu` when the first screen needs it.
