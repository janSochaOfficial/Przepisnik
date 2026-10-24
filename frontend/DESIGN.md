# Przepiśnik design system

The visual direction of the app, set once in task F0 (TECHNICAL §4.3). F3–F6 follow this file and
**do not restyle**: they compose the primitives in `src/lib/components/ui` and the shell in
`src/lib/components/shell`. If something you need is missing, build it from the tokens below and
report it in your task's Report, rather than inventing new colours, sizes or shadows.

Live reference: `npm run dev`, then open **`/ui`** (the showcase renders every primitive in every
state; it is excluded from production builds).

---

## 1. Brief

- **Subject:** a home kitchen notebook for Polish cooks: recipes, their variants, the fridge and
  the shopping list.
- **Audience:** people cooking at home, often with wet or floury hands, phone propped up on the
  counter at arm's length, glancing at it between steps. Some of them share a fridge with family.
- **Primary job:** read amounts and steps at a glance and act with one thumb (scale, pick a
  substitute, tick off, undo).

## 2. Direction: "Bolesławiec"

The app borrows from **Bolesławiec stoneware** (_ceramika bolesławiecka_), the cobalt-on-grey
pottery found in many Polish kitchens: a cool stoneware ground, cobalt blue as the only strong
colour, small dot-and-ring stamps (the _oczko_, "peacock eye"), with a touch of green. Text is set
like a printed cookbook: a bookish serif for titles, a highly legible sans for everything you
read while cooking.

### What was rejected as too generic

| Rejected                                                                 | Why                                                                                                                                             |
| ------------------------------------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------- |
| Warm cream paper + serif + terracotta accent ("handwritten recipe card") | The default look of any food or "notebook" page today; also reads as an AI tell. Our ground is a **cool** stoneware grey, the accent is cobalt. |
| Handwriting / script fonts for a "notebook" feel                         | Hard to read at arm's length, poor Polish diacritics, gimmicky after one screen.                                                                |
| Fresh-green "healthy food" palette with photo-heavy cards                | Every recipe app; it competes with users' own photos. Green is only a small secondary (dill).                                                   |
| Near-black dark mode with one neon accent                                | Generic; the dark mode here is deep cobalt night, not grey-black.                                                                               |
| SaaS card kit (identical cards, one radius, grey shadow everywhere)      | Radius and elevation follow hierarchy (§6); only overlays have shadows.                                                                         |
| ALL-CAPS eyebrow labels, `01 / 02` numbering, `→` on buttons             | Template chrome. Numbers appear only where content is a sequence (instruction steps).                                                           |
| Inter / system-ui                                                        | Neutral default; Atkinson Hyperlegible Next was chosen for a reason (§4).                                                                       |

## 3. Colour

Five named colours make the palette; everything else is a tint of them. Each is a CSS custom
property in `src/app.css` (`--rl-*`) and a Tailwind colour (`bg-cobalt`, `text-muted`,
`border-line` …). **Never write a hex value in a component.** Light and dark come from
`prefers-color-scheme`; there is no manual switch in v1.

| Name                             | Role                                                                 | Light     | Dark      |
| -------------------------------- | -------------------------------------------------------------------- | --------- | --------- |
| **Kamionka** (stoneware) `paper` | page background                                                      | `#EEF1F0` | `#0F1420` |
| **Atrament** (ink) `ink`         | text, icons, the toast background                                    | `#1B2333` | `#E5E9F0` |
| **Kobalt** `cobalt`              | the one accent: primary buttons, links, focus, selection, active nav | `#1F4FA8` | `#8DAEF2` |
| **Koper** (dill) `dill`          | success, "in the fridge", the centre of the oczko                    | `#2E6A3A` | `#8CCB93` |
| **Burak** (beet) `beet`          | danger and errors only                                               | `#A3214A` | `#F28BA9` |

Supporting tokens (derived; use them, do not add more):

| Token                     | Use                                                         | Light              | Dark                  |
| ------------------------- | ----------------------------------------------------------- | ------------------ | --------------------- |
| `surface`                 | raised areas: fields, cards, sheets, tab bar                | `#FFFFFF`          | `#18202F`             |
| `sunken`                  | skeletons, slider track, disabled fields, count badges      | `#E3E8E7`          | `#0A0E17`             |
| `muted`                   | secondary text (hints, meta, inactive tabs)                 | `#4F5A6B`          | `#A5AFBF`             |
| `line`                    | dividers, card borders                                      | `#C3CBD2`          | `#2E3A50`             |
| `line-strong`             | borders of interactive controls (≥ 3:1)                     | `#737E8D`          | `#6B7891`             |
| `cobalt-strong`           | hover of primary                                            | `#173D85`          | `#B3CAF7`             |
| `on-cobalt`               | text on cobalt                                              | `#FFFFFF`          | `#0D1730`             |
| `cobalt-wash`             | selected row, active tab-bar pill, hover of secondary/ghost | `#DFE7F6`          | `#1D2A47`             |
| `dill-wash`               | success backgrounds                                         | `#DDEEDF`          | `#173023`             |
| `beet-strong` / `on-beet` | hover of danger / text on beet                              | `#85193B` / `#FFF` | `#F7B0C5` / `#2A0814` |
| `beet-wash`               | error message background                                    | `#F7E1E8`          | `#3A1624`             |
| `scrim`                   | behind modal overlays                                       | ink 45 %           | black 60 %            |

Contrast (WCAG AA, checked): ink on paper 13.8:1 (dark 15.1); muted on paper 6.1 (8.3); cobalt on
paper 6.7 (8.3), on surface 7.7; dill on paper 5.7; beet on paper 6.4; on-cobalt on cobalt 7.7
(8.0); line-strong vs surface 4.1 (3.7). The Playwright a11y test runs axe in both modes.

Rules:

- **Cobalt is rare.** One primary button per view; links and the focus ring are the other uses.
- **Beet means danger.** Never use it for decoration, favourites or "hot" tags.
- Status is never colour alone: errors carry an icon and text, selection has a check or a fill.
- Opacity modifiers (`bg-paper/95`, `border-beet/40`) are allowed for overlays and borders only.

## 4. Type

| Role                                                      | Family                                                                                       | Tailwind                              | Notes                                                                                  |
| --------------------------------------------------------- | -------------------------------------------------------------------------------------------- | ------------------------------------- | -------------------------------------------------------------------------------------- |
| Titles (h1–h3, dialog titles, recipe names, empty states) | **Literata** (variable, self-hosted via `@fontsource-variable/literata`)                     | `font-serif` (automatic on `h1`–`h3`) | weight 600, `text-wrap: balance`                                                       |
| Everything else: body, labels, buttons, amounts           | **Atkinson Hyperlegible Next** (variable, `@fontsource-variable/atkinson-hyperlegible-next`) | `font-sans` (default)                 | designed for low vision: `1 l I` and `0 O` are distinct, which matters for "1 l mleka" |

Both include `latin-ext` (all Polish letters). No other families.

**Scale** (17 px base, ratio ≈ 1.2; Tailwind's default sizes are replaced):

| Class       | Size  | Line height | Use                                                   |
| ----------- | ----- | ----------- | ----------------------------------------------------- |
| `text-xs`   | 13 px | 1.4         | tab-bar labels, captions under the brand only         |
| `text-sm`   | 15 px | 1.45        | field labels, hints, errors, meta lines, chips        |
| `text-base` | 17 px | 1.5         | body, inputs, buttons, list rows (the default)        |
| `text-lg`   | 20 px | 1.35        | sheet titles, large buttons, card titles              |
| `text-xl`   | 24 px | 1.25        | page title on phones, dialog title, empty-state title |
| `text-2xl`  | 29 px | 1.2         | section titles in long pages (h2)                     |
| `text-3xl`  | 35 px | 1.15        | page title from 1024 px                               |

- Weights: 400 body, 600 (`font-semibold`) for labels, buttons, active states and titles. No
  700+, no italics for emphasis, no all-caps.
- Amounts and percentages use `tabular` (a utility in `app.css`: tabular figures) so columns align.
- Line length: body text stays under ~70 characters (`Page` width `reading` handles it).
- Sentence case everywhere, including buttons and tabs.

## 5. Spacing, size and touch

- Tailwind's 4 px spacing scale. Common steps: `gap-2` (8) between buttons, `gap-4`/`gap-5`
  (16/20) between fields, `gap-10` (40) between page sections.
- Page gutters: 16 px on phones, 24 px from 768 px, 40 px from 1024 px (all in `Page`).
- **Touch:** every interactive element is **≥ 44 × 44 px** (`min-h-11`, `size-11`). Text inputs
  and selects are 48 px (`min-h-12`). The primary action of a screen may use `size="lg"` (52 px).
  Chips are 36 px tall but get an invisible 44 px hit area; keep `gap-2` between them.
- Focus: one style for everything: a 3 px cobalt outline, 2 px offset (`:focus-visible` in
  `app.css`). Never remove it; if a control draws its own box (TagInput), show the ring on the box.

## 6. Shape and elevation

| Token             | Radius | Use                                            |
| ----------------- | ------ | ---------------------------------------------- |
| `rounded-control` | 10 px  | buttons, inputs, list rows, menu items         |
| `rounded-card`    | 16 px  | cards, popovers, toasts, error blocks          |
| `rounded-sheet`   | 20 px  | bottom-sheet top edge, dialogs, confirm dialog |
| `rounded-full`    | pill   | chips, avatars, switch, count badges           |

- Elevation by meaning, not decoration: `shadow-raised` only on the slider thumb and switch thumb;
  `shadow-overlay` only on things floating above the page (popover, sheet, dialog, toast, menus).
  Cards have a `border-line` border and no shadow.

## 7. Layout

Mobile first; designed at **360 px**. Breakpoints: `md` 768 px, `lg` 1024 px.

```
Phone (< 1024 px)                       Desktop (≥ 1024 px)
┌──────────────────────────┐            ┌─────────┬──────────────────────────────────┐
│ ←  Title            [⋯]  │ sticky     │ ◎ Prze- │   ←  Title (35 px serif)   [⋯]   │
├──────────────────────────┤ header     │   piśnik│                                  │
│ content, 16 px gutters   │            │         │   content, max 45 rem (reading)  │
│                          │            │ Przepisy│   or 72 rem (wide: card grids)   │
│                          │            │ Lodówka │                                  │
│                          │            │ Lista … │                                  │
│ ┌──────────────────────┐ │            │ Profil  │  ┌────────────────────────────┐  │
│ │ Anuluj        Zapisz │ │ ActionBar  │  240 px │  │ Anuluj              Zapisz │  │
├─┴──────────────────────┴─┤            │  rail   │  └────────────────────────────┘  │
│ 📖    ▢     🧺     ◯     │ tab bar    └─────────┴──────────────────────────────────┘
│ Przepisy Lodówka …       │ 64 px + safe area
└──────────────────────────┘
```

- **Alignment:** left-aligned everywhere. Centred only in EmptyState and the error block.
- `md` (768 px): still the tab bar; two columns where they help (recipe view: ingredients beside
  steps; catalog: two-column cards). Dialogs become centred cards; BottomSheet becomes a popover.
- `lg` (1024 px): the tab bar turns into the side rail (one `<nav>`, same links, same order).
- Safe areas: `viewport-fit=cover` is set; the shell pads with `--rl-safe-*`. Pages never pad for
  the tab bar or notch themselves.
- Shell metrics are tokens: `--rl-header-h` 56 px, `--rl-tabbar-h` 64 px, `--rl-rail-w` 240 px.
  `ActionBar` publishes `--rl-actionbar-h` while mounted (toasts rise above it).

## 8. Signature: the oczko

The one memorable element is the **oczko** (`Oczko.svelte`): a dill dot, a cobalt ring, a ring
of twelve cobalt dots and a thin outer ring. It appears in exactly two places:

1. the brand mark at the top of the desktop rail;
2. `EmptyState`, when no more specific icon is given.

Do not use it as a bullet, a loader, a background pattern or on cards. Everything around it stays
quiet: no gradients, no textures, no illustrations.

## 9. Motion

- Only motion that answers an action: the sheet slides up (200 ms), toasts fade in (180 ms),
  colours change on hover/press (150 ms). No entrance animations on pages or lists.
- `prefers-reduced-motion: reduce` turns every animation and transition off (global rule in
  `app.css`). Skeletons then stop pulsing.

## 10. Components

Import primitives from `$lib/components/ui` and the shell from `$lib/components/shell`. All texts
built into them live in `t.common.ui` / `t.common.actions` / `t.common.shell`.

### Shell

| Component    | Use                                                                                                                                                                                             |
| ------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AppShell`   | Mounted by `routes/+layout.svelte`. Tab bar / rail, skip link, hides itself on `/logowanie` and `/witaj`. Active item = `activeRoute(pathname)` (`/warianty/*` counts as Przepisy).             |
| `Page`       | **Every screen starts with it.** `title` (the page's only h1), `back?` (resolved href or function), `subtitle?`, `actions?` snippet (≤ 2 `IconButton`s), `width` `reading` (default) or `wide`. |
| `PageHeader` | Used by `Page`; use alone only inside a custom layout. Sticky on phones.                                                                                                                        |
| `ActionBar`  | Last child of `Page` for the screen's main actions (recipe view, editors). Sticky above the tab bar. At most one primary `Button`, placed last (it is pushed right).                            |

### Actions

| Component                    | Props (main)                                                                                                | Rules                                                                                                                                                                                              |
| ---------------------------- | ----------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `Button`                     | `variant` `primary`/`secondary`/`ghost`/`danger`, `size` `md`/`lg`, `loading`, `icon`, `href`, `fullWidth`  | Label = verb + object ("Zapisz przepis"). One primary per view. `danger` only for destructive actions, normally inside `ConfirmDialog`. `loading` keeps the label, shows a spinner, blocks clicks. |
| `IconButton`                 | `icon`, `label` (required, the accessible name and tooltip), `variant` (default `ghost`), `loading`, `href` | For toolbars and headers. If a user might not know the icon, use a `Button` instead.                                                                                                               |
| `buttonClass(variant, size)` | helper                                                                                                      | To style a non-button element (bits-ui triggers) exactly like a Button.                                                                                                                            |

### Fields

All fields take `label` (required), `hint?`, `error?`, `optional?` (shows "(opcjonalnie)";
required is the default and has no mark), `hideLabel?`. Errors are shown under the field and
linked with `aria-describedby`; pass `t.errors[...]` or the API's field errors.

| Component     | Notes                                                                                                                                                                                                                                                                                                                                                            |
| ------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `TextField`   | `bind:value`, `type` (text/email/search/url/password/tel), `suffix` (e.g. a unit).                                                                                                                                                                                                                                                                               |
| `NumberField` | **Use for every amount.** `bind:value: number \| null`, `min`, `max`, `suffix`, `onValueChange`. Accepts `1,5`, `1.5`, `,5`, `1 250`; shows a decimal comma; invalid text → its own error and `value = null`. Text input with `inputmode="decimal"`, never `type="number"`. Helpers `parseDecimal` / `formatDecimal` are exported.                               |
| `TextArea`    | `bind:value`, `rows` (min height); grows with content.                                                                                                                                                                                                                                                                                                           |
| `Select`      | `items: Option[]`, `bind:value: string`. Short fixed lists (units, fridges).                                                                                                                                                                                                                                                                                     |
| `Combobox`    | `search(query, signal) → Promise<Option[]>`, `bind:value: Option \| null`, `onCreate?(query)`, `debounceMs` (250), `minChars` (1). Server-backed search (products, tags). With `onCreate` a footer item "Dodaj „…”" appears unless an option has exactly that label; the caller creates the entity and then sets `value`. Respect the `AbortSignal` in `search`. |
| `TagInput`    | `bind:value: string[]`, `suggestions?`. Enter or comma adds, Backspace removes the last, de-duplicated case-insensitively. Optional by default.                                                                                                                                                                                                                  |
| `Field`       | The label/hint/error wrapper, for a custom control only.                                                                                                                                                                                                                                                                                                         |

### Choices

| Component | Notes                                                                                                                                                                                      |
| --------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `Chip`    | Static tag (no handlers); toggle filter (`selected` + `onclick`, uses `aria-pressed`); removable (`onRemove` + `text`). Wrap a set of filter chips in `role="group"` with an `aria-label`. |
| `Switch`  | `label`, `description?`, `bind:checked`. Applies at once (no Save).                                                                                                                        |
| `Slider`  | `label`, `bind:value`, defaults `0–100`, `step 5`, value printed as "45 %"; `onValueCommit` fires once on release — save there, not in `onValueChange`.                                    |
| `Tabs`    | `items: TabItem[]` (`count?` badge), `bind:value`, `label`, `panel` snippet (renders only the active tab). For sub-views of one page (Wszystkie / Moje przepisy / Moje warianty).          |

### Overlays

| Component       | When                                                                                                                                                                                                                                                                                                                             |
| --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `BottomSheet`   | A small picker tied to one control: ingredient options, scaling. **Sheet below 768 px, popover from 768 px**, one component. `trigger` snippet is required (spread `props` on your button), `title`, `bind:open`. Keep content short (a list of choices or one field); a long form is a `Dialog`.                                |
| `Dialog`        | A task needing full attention (deduction). Full-screen on phones, a card from 768 px. `title`, `description?`, `footer` snippet (primary last), `size` `md`/`lg`, optional `trigger`.                                                                                                                                            |
| `ConfirmDialog` | Before destructive or irreversible actions. `confirmLabel` names the action, `tone` `danger` (default) or `primary`, `onConfirm` may be async (spinner; stays open if it throws). Prefer **undo with a toast** over a confirmation when the action can be undone (deleting a fridge item, deducting).                            |
| `toast`         | `toast.show(message, { action?, tone?, duration? })`, `toast.success`, `toast.error`. 5 s, or 8 s with an action, paused while hovered/focused, max 3. Undo pattern: `toast.show(t.x.deleted, { action: { label: t.common.actions.undo, onAction: undo } })`. Rendered by `<Toaster />` in the root layout; don't mount another. |

### Feedback

| Component      | Notes                                                                                                                                                                                                              |
| -------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `ErrorMessage` | `error` (anything: an `ApiError` shows `t.errors[code]`, anything else the generic server message; `null` renders nothing), `retry?`, `variant` `inline` (above a form) / `block` (a section that failed to load). |
| `EmptyState`   | `title` (what is empty), `description` (what to do), `action` snippet (the button that does it), `icon?` (only when more specific than the oczko, e.g. search), `level` 2/3.                                       |
| `Skeleton`     | `shape` `line`/`block`/`circle`, sized with classes. Mirror the real layout; the container sets `aria-busy="true"`. Use for loads > 300 ms.                                                                        |
| `Avatar`       | `name`, `src?`, `size` `sm`/`md`/`lg`, `decorative` when the name is printed next to it. Initials fallback.                                                                                                        |
| `Spinner`      | Inside buttons only; pages load with skeletons.                                                                                                                                                                    |

## 11. Wording

- The UI speaks Polish, plainly, in sentence case. Buttons are verbs naming the result:
  "Zapisz przepis", "Odejmij z lodówki", "Dodaj produkt". The toast repeats the verb in the past:
  "Zapisano przepis.", "Odjęto z lodówki.".
- Generic words come from `t.common.actions` (Zapisz, Anuluj, Usuń, Cofnij, Wróć, Zamknij …) so
  the same action has the same name everywhere.
- Errors say what happened and what to do, without apologising (see `t.errors`).
- Empty states invite an action ("Dodaj pierwszy produkt, a przepisy pokażą, czego brakuje.").
- Numbers and dates use `pl-PL` (`1,5`, `22.10.2026`) via `lib/domain/format.ts` or
  `formatDecimal`; a space before `%` and units ("45 %", "200 g").

## 12. Checklist for a new screen

1. `<Page title=…>` with `back` for every non-root screen; one `h1`.
2. Only tokens (`bg-surface`, `text-muted`, `rounded-card` …); no hex, no new shadows or radii.
3. Primitives for every control; `NumberField` for amounts; `BottomSheet` for per-control pickers.
4. Loading = `Skeleton`, failure = `ErrorMessage`, nothing yet = `EmptyState` with an action.
5. Destructive + undoable → do it and offer undo in a toast; not undoable → `ConfirmDialog`.
6. Check at 360 px and 1280 px, light and dark, keyboard only; add an axe check to the page's
   Playwright test (see `tests/integration/ui-showcase.spec.ts` for the pattern).
7. Run the svelte autofixer (`npm run autofix:svelte -- <files>`).
