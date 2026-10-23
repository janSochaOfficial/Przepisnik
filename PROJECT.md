# Przepiśnik: project goals and functional specification

> Working display name **Przepiśnik** (changeable, see D-01). Code name: `RecipesList`.
> This document says **what** the app does and **why**. [TECHNICAL.md](TECHNICAL.md) says **how** it is built.
> The API contract is in [docs/API.md](docs/API.md), and the delivery plan is in [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md).

## 1. Vision

This is a web app, built for phones first, for keeping recipes and knowing what is in the fridge.
Recipes grow through **variants**: small, tracked changes to a base recipe, not copies of it.
The app scales a recipe from what you actually have ("I have 600 g of chicken"). It shows amounts the way a home cook measures them (glasses, spoons, pinches, small/medium/large onions). It can deduct what you cooked from a shared household fridge.

### Language rule
- **Everything the user sees is in Polish**: UI, validation messages, seed data (products, tags, utensils) and units.
- **Code, identifiers, comments, commit messages, API and documentation are in English.**
- Numbers and dates use Polish formatting (`pl-PL`: decimal comma, `22.10.2026`).

### Goals (v1)
1. Write down recipes with text and photos mixed together, plus tags and required utensils. Each recipe and variant is private, shared with your fridges, or public, and can be marked as a favourite (♥).
2. Keep variants as diffs against the base recipe, and let a cook mix variant choices ingredient by ingredient. The app remembers those choices.
3. Scale a recipe from any one ingredient, with kitchen-friendly units.
4. Track one or more fridges, shared between people, with "how much is left" percentages.
5. Deduct a cooked recipe from a fridge, and push the missing items to a shopping list that can be shared to Google Keep.
6. Export and import recipes as self-contained JSON.

### Non-goals (v1): see also §12
Servings-based scaling, expiry dates, a direct Google Keep API, offline/PWA, languages other than Polish, native apps, ratings and comments.

---

## 2. Users and authentication

- Users sign in **only with Google**. On first login the user picks a **nickname** (unique, case-insensitive, 3–30 characters, letters/digits/`_`/`-`, Polish letters allowed).
- The nickname is public. The Google email is stored but **never shown to other users**.
- Sessions last 30 days and slide (renewed on use). The app has an explicit "Wyloguj" (log out).
- **Admin** is a flag on the user, granted through configuration (`Admin__Emails`). Admins can edit and merge products in the shared catalog (§7.3).
- Per-user settings:
  - `liquidUnits`: `kitchen` (glasses, the default) or `metric` (ml). See §5.4.
  - `defaultFridgeId`: which fridge is selected first in deduction and shopping-list dialogs.

## 3. Navigation (Polish UI labels)

On mobile the bottom tab bar shows **Przepisy** · **Lodówka** · **Lista zakupów** · **Profil**. On desktop the same items sit in a side rail.

| Screen | Polish label | Content |
|---|---|---|
| Recipe catalog | **Przepisy** | Every recipe I can see (§4.4). Search, tag filter, sub-tabs **Wszystkie / Moje przepisy / Moje warianty**. **Moje przepisy** is split into **Utworzone** (written by me) and **Ulubione** (my ♥ recipes and variants, §4.6) |
| Recipe view ("cook mode") | (recipe title) | Variant picker, scaling, ingredients, prep, instructions, ♥, actions at the bottom |
| Recipe editor | **Nowy przepis / Edytuj przepis** | Full editor (§4), including the visibility setting; editing is restricted by §4.5 |
| Variant editor | **Nowy wariant / Edytuj wariant** | Diff editor on top of the base recipe (§6) |
| Fridge | **Lodówka** | Fridge switcher, items, members, invites (§8) |
| Shopping list | **Lista zakupów** | List for the selected fridge, share/copy (§10) |
| Profile | **Profil** | Nickname, unit setting, default fridge, log out, import recipe |

"Moje przepisy → Utworzone" = recipes I wrote. "Moje przepisy → Ulubione" = recipes and variants I marked with ♥. "Moje warianty" = variants I created, on anyone's recipe. A variant card (in Ulubione or Moje warianty) links to its base recipe with that variant preselected.

## 4. Recipes

### 4.1 Structure
| Field | Notes |
|---|---|
| Title | required, ≤ 150 characters |
| Description | optional short intro, plain text, ≤ 2000 characters |
| Cover photo | optional |
| Prep time / cook time | minutes, optional, used for the auto tag |
| Tags | many; see §4.3 |
| Utensils (**Przybory**) | many; from a growing shared catalog (like tags), e.g. "piekarnik", "blender", "patelnia" |
| **Składniki** (ingredients) | ordered list (§4.2) |
| **Przygotowanie** (prep) | ordered steps; each step is a list of **content blocks** |
| **Wykonanie** (instructions) | ordered steps; each step is a list of **content blocks** |

**Content blocks** let photos sit in the middle of the text. A step is a sequence of `text` blocks (plain text, line breaks kept) and `image` blocks (photo plus optional caption). The editor can insert a photo between any two paragraphs of a step. Photos are uploaded first and referenced by ID.

### 4.2 Ingredients
Each ingredient row has:
- **product**: a reference to the shared product catalog (§7). A product can be created inline from the editor.
- **quantity** as the author entered it: a value plus a unit (`g`, `kg`, `ml`, `l`, `szklanka`, `łyżka`, `łyżeczka`, `kropla`, `szczypta`, `szt.`), plus a piece size (`mała`/`średnia`/`duża`) for products that have sizes.
- or **no quantity**, meaning "do smaku" (to taste). It is never scaled or deducted.
- **note** (e.g. "pokrojona w kostkę"), and an **optional** flag ("opcjonalnie").

The server stores a canonical **base amount** for each ingredient (g, ml or medium-equivalent pieces; §5.1). Scaling, variant matching and fridge deduction all use the base amount.
A unit whose kind differs from the product's kind (e.g. `łyżka` of butter, which is a mass product) is accepted only if the product has the needed conversion data (density or piece weight). Otherwise the editor asks for another unit.

### 4.3 Tags
- Tags come from one **shared, growing catalog**: free-form with autocomplete. A new name creates a tag, and duplicates are matched case- and diacritic-insensitively.
- The catalog is seeded with common tags (`obiad`, `śniadanie`, `kolacja`, `deser`, `zupa`, `wege`, `wegańskie`, `bez glutenu`, `na imprezę`, `z piekarnika`, …).
- **System tag `szybkie`** is computed, never chosen by hand: it is present when at least one time is set and `prepTime + cookTime ≤ 30 min`, with a missing time counted as 0 (D-09). It is recomputed whenever times change, including the effective times of a variant.

### 4.4 Visibility and ownership
Every recipe has a **visibility** (`widoczność`) setting, chosen in the editor (D-21):

| Value | Polish label | Who can see it |
|---|---|---|
| `private` | **Prywatny** | only the author |
| `fridges` | **Moje lodówki** | the author and everyone who shares **at least one fridge** with the author (D-22). This is computed live: joining or leaving a fridge changes access immediately |
| `public` | **Publiczny** | every logged-in user |

- New recipes are **private** by default (D-23).
- Anyone who can see a recipe can open it, cook from it, favourite it, export it and **create variants** of it. Only the author can edit, delete it or change its visibility.
- A recipe the viewer cannot see answers "not found", so its existence never leaks.
- **Making a recipe public** asks for confirmation: "Publicznego przepisu nie można edytować — zmiany będą możliwe tylko przez warianty." (A public recipe can't be edited; changes are only possible through variants.)
- **Narrowing** visibility (public → fridges/private, fridges → private) is **blocked while other users have variants** of the recipe (D-24). Without foreign variants it is allowed. The author's own variants keep working, but become visible only to people who can still see the recipe.
- **Deleting** a recipe that has variants by other users does not destroy their work. The recipe is **archived** instead: it is hidden from the catalog and search, but still reachable read-only by the authors of those variants. A recipe without foreign variants is deleted for real.

### 4.5 Editing a recipe directly (without a variant)
Only the author can edit a recipe directly, and only under these rules (D-25):

| Recipe state | What "Edytuj" does |
|---|---|
| `public` | Editing is **not possible**. The menu offers **"Utwórz wariant"** instead, plus "Zmień widoczność", which works only when no one else has variants (§4.4) |
| `private` / `fridges`, **no variants** | Normal editing |
| `private` / `fridges`, **has variants** (anyone's, including the author's own) | Editing is allowed, but on save the app warns: **"Zapisanie zmian usunie wszystkie warianty tego przepisu (N, w tym M innych osób). Kontynuować?"** ("Saving will delete all N variants of this recipe, M of them by other people. Continue?"). On confirmation the recipe is saved and **all its variants are deleted** in the same operation |

Changing visibility is a separate action, not an edit, so it never deletes variants.
Because a base recipe can only change when it has no variants (or they are deleted), variants never point at rows that no longer exist.

### 4.6 Favourites (♥)
- Any recipe or variant the user can see can be marked as a favourite with a heart icon. The heart appears on recipe cards, in the recipe view header, and next to each variant name (in the variant picker and on variant cards).
- **Moje przepisy → Ulubione** lists favourite recipes and variants together, most recently favourited first. A variant entry shows "wariant *X* · przepis *Y*" and opens the recipe with that variant preselected.
- Favourites the user can no longer see (visibility changed, fridge left) are **hidden, not deleted**, and come back when access returns. Favourites of deleted recipes or variants are removed.
- Favourite variants rank higher in the variant auto-match (§6.3).
- Favourites are private: nobody else sees who favourited what.

### 4.7 Export and import (JSON)
- **Export** is available to anyone who can see the recipe. It produces one self-contained file: the recipe, **all its variants that the exporter can see**, and every referenced photo embedded as base64. Products, tags and utensils are referenced **by name and properties**, not by database ID. Format: [docs/API.md §8](docs/API.md).
- **Import** creates a **new private recipe owned by the importer**, together with all its variants, also private and owned by the importer. Each keeps an "originally by @nick" note. Products, tags and utensils are matched by normalized name and created if missing. The format is versioned (`formatVersion: 1`), and the file limit is 25 MB.

## 5. Quantities, units and scaling

### 5.1 Canonical model
| Measure kind | Base unit | Examples |
|---|---|---|
| `mass` | gram | meat, flour, cheese, butter |
| `volume` | millilitre | milk, oil, cream, spices* |
| `count` | medium-equivalent piece | onion, egg, garlic clove, can |

\* Spices are measured by volume (spoons/pinches), so their products are `volume`.

Each product also has a **display class**, which decides how amounts are *shown* after scaling:
`weight` · `liquid` · `spice` · `piece`.

### 5.2 Conversion constants (fixed, not looked up online at runtime; D-07)
| Unit (PL) | Value |
|---|---|
| szklanka (glass) | 250 ml |
| łyżka (tablespoon) | 15 ml |
| łyżeczka (teaspoon) | 5 ml |
| kropla (drop) | 0.05 ml |
| szczypta (pinch) | 0.3 ml |
| kg / l | 1000 g / 1000 ml |
| piece sizes | mała **0.75×**, średnia **1×**, duża **1.3×** of a medium piece |

Converting between mass and volume uses the product's `gramsPerMl`. Converting between count and mass uses `gramsPerPiece` (the weight of a medium piece).
The same constants live in both the backend and the frontend. They are checked against the shared test vectors in `shared/test-vectors/quantities/` so the two cannot drift apart.

### 5.3 Scaling
- In the recipe view the user taps any ingredient with a quantity and enters **"Mam: [ilość] [jednostka]"** ("I have: amount, unit"). The factor is `entered base amount / recipe base amount`, and **every** ingredient with a quantity is multiplied by it. "Do smaku" ingredients are unchanged.
- For piece products the user enters a count and a size (e.g. "2 duże").
- A "Przywróć" (reset) button returns to factor 1. The last scaling choice is remembered per user per recipe (§6.6).
- **At factor 1, amounts are shown exactly as the author entered them.** Once scaled, they are shown with the rules in §5.4.

### 5.4 Display rules after scaling (fully specified so they can be tested)
Numbers use a decimal comma, and fractions are shown as glyphs (¼ ½ ¾). Polish plural forms are applied (§5.5).

**weight**: `< 10 g` → step 1 g · `< 100 g` → step 5 g · `< 1000 g` → step 10 g · `≥ 1000 g` → kg with 0.05 kg step (`1,25 kg`).

**liquid**, with `v` in ml:
1. `v > 1000` → litres, step 0.1 l (`1,2 l`), in both settings.
2. `62.5 ≤ v ≤ 1000`:
   - `kitchen` setting: glasses rounded to the nearest ¼ (`¾ szklanki`, `1 ½ szklanki`, `4 szklanki`).
   - `metric` setting: ml rounded to 10 ml (`250 ml`).
3. `0.625 ≤ v < 62.5` → spoons. Round to the nearest ¼ teaspoon (1.25 ml), then split greedily into whole tablespoons and the rest in teaspoons (`2 łyżki + 1 ½ łyżeczki`, `¾ łyżeczki`).
4. `v < 0.625` → drops, `max(1, round(v / 0.05))` (`8 kropli`).

**spice** (volume): the same as liquid, but **never glasses, ml or litres**. Large amounts stay in tablespoons. Below 0.625 ml the amount is shown in **pinches** instead of drops: `max(1, round(v / 0.3))` (`1 szczypta`, `2 szczypty`).

**piece** with sizes (`hasSizes = true`), target `t` in medium-equivalents:
- `t < 0.3` → `¼ średniej` · `t < 0.625` → `½ średniej`
- otherwise find the combination of large/medium/small pieces (L=1.3, M=1, S=0.75) with the **fewest pieces** whose sum is within **±10 %** of `t`. Ties go to the smallest error, then to the combination with more medium pieces. If no combination is within ±10 %, the combination with the smallest error is used, searching up to `ceil(t / 0.75) + 1` pieces.
  - Examples: `2.8` → `2 duże` (2.6, −7 %) · `1.4` → `1 duża` · `3.5` → `2 duże + 1 średnia` (3.6).

**piece** without sizes (eggs, cans, cloves): round to a whole number, minimum 1. Products flagged `allowHalf` (e.g. lemon) round to the nearest ½.

Every scaled amount gets a `~` prefix when the shown value differs from the exact one by more than 5 %.

**Inventory format** (fridge items, shopping list, deduction dialog "available" column): packages are bought in metric units, so `liquid` and `spice` amounts are **always metric** there, whatever the user setting is. Steps: `< 10 ml` → 1 ml · `< 100 ml` → 5 ml · `≤ 1000 ml` → 10 ml · above that, litres with a 0.1 l step. `weight` and `piece` use the rules above (pieces without the size combination: whole numbers, or ½ when `allowHalf`).

### 5.5 Polish plurals
Units and piece names have four forms: **one / few / many / fraction**:
- `szklanka / szklanki / szklanek / szklanki`
- `łyżka / łyżki / łyżek / łyżki`
- `łyżeczka / łyżeczki / łyżeczek / łyżeczki`
- `kropla / krople / kropli / kropli`
- `szczypta / szczypty / szczypt / szczypty`
- sizes: `mała / małe / małych`, and the same pattern for `średnia` and `duża`

Rules for integers: 1 → *one*; a number ending in 2–4 but not 12–14 → *few*; everything else → *many*. Any amount with a fractional part → *fraction*.
Products may define their own piece forms (`ząbek / ząbki / ząbków / ząbka`); the default is `szt.`.

## 6. Variants

### 6.1 Concept
A **variant** (`wariant`) belongs to exactly one base recipe. It is stored as a **list of operations** against that recipe, never as a copy of it. Any user who can see a recipe can create a variant of it (§4.4). Variants are flat: there are no variants of variants.

A variant has a name (e.g. "Wersja wege", "Ostrzejsza"), an optional description, an author, a **visibility**, and these operations.

**Variant visibility** uses the same three values as recipes (`private` / `fridges` / `public`), evaluated against the **variant's author**, and is private by default. A viewer sees a variant only if they can see **both** the base recipe **and** the variant. For example, a public variant of a fridge-only recipe is still visible only to that recipe's audience. The variant author can change it at any time; the editor shows a hint when the recipe's own visibility is narrower. Variants hidden from a viewer are left out of the variant picker, the matrix and exports for that viewer.

| Target | Operations |
|---|---|
| Ingredients | **modify** (product and/or quantity and/or note/optional flag), **remove**, **add** (after a given ingredient) |
| Prep / instruction steps | **replace** content, **remove**, **add** (after a given step) |
| Utensils | add / remove |
| Times | override prep and/or cook time |

**Resolving** a variant means applying its operations to the current base recipe to get a full recipe.

### 6.2 Variant editor
The editor opens the base recipe as an editable copy. When the user saves, the client sends the **edited result**, and the server **computes the diff** against the base (D-12). Users never deal with operations directly. Unchanged rows produce no operations.
In v1 a variant can add, change and remove rows, but it **cannot reorder the base recipe's rows**. The variant editor locks the order of base rows, and new rows can be inserted anywhere.

### 6.3 Variant auto-match ("dobieranie wariantów")
This is the core interaction in the recipe view.

0. **Scope.** Only variants the viewer can see (§6.1) take part.
1. **Slots.** Every base ingredient is a slot. Ingredients that variants *add* are grouped into slots by product: two variants that both add bell pepper share one slot.
2. **Options.** A slot's options are the distinct values it takes across the base and all variants. Each option is a product plus a base amount (equal within 1 %), or **"brak"** (the ingredient is absent or removed). Each option knows which variants support it.
3. **Picks.** Tapping an ingredient opens a sheet that lists its options. Picking one makes it a **user pick** (pinned).
4. **Compatible variants** are the variants (the base recipe counts as one) that agree with *all* current user picks.
5. **Best variant** is the top compatible variant, ranked by:
   1. most matches with the user's remembered picks for this recipe,
   2. variants the user has marked as favourite (♥, §4.6),
   3. a product-preference score (§6.6),
   4. cook count (how many times it was deducted from a fridge),
   5. the base recipe before other variants, then older variants first.
6. **Auto picks.** Every unpinned slot takes the best variant's option, shown with an "auto" marker.
7. **Ordering in the sheet.** Options supported by a compatible variant come first, then the rest, which are dimmed but still selectable (ranked, not hidden; D-04). Within each group, options with more supporting variants come first.
8. **Custom mix.** If the user picks options that no single variant contains, the view shows **"Własna kombinacja"**. Unpinned slots and the steps come from the variant that matches the most pins (same tie-breakers).
9. The steps (prep and instructions), utensils and times always come from the best variant (or, for a custom mix, the variant that matches the most pins).
10. The last entry in every option sheet is **"+ Utwórz nowy wariant z bieżącego wyboru"** ("create a new variant from the current selection"). It opens the variant editor prefilled with the current resolved recipe (all picks, plus steps from the best variant), and the user names it and saves.
11. A **"Wyczyść wybór"** (clear selection) action removes all user picks.

### 6.4 Base recipe changes
A base recipe never changes while it has variants: public recipes are read-only, and editing a private or fridge-only recipe deletes all its variants (§4.5). So variant operations always point at rows that exist. No revision tracking or "stale change" handling is needed.

### 6.5 Ownership
Only the author can edit or delete a variant, or change its visibility. Variants can be edited at any time, whatever the visibility. Deleting a variant, whether directly or because of §4.5, removes it from every user's remembered picks and favourites.
A variant author who loses access to the base recipe (e.g. by leaving the fridge it was shared through) still sees the variant in "Moje warianty" with the note "Przepis bazowy jest niedostępny" (the base recipe is unavailable), and can only delete it.

### 6.6 Selection memory
Remembered picks are stored **per user on the server** (D-05); the client keeps a cache of them.
- **Per recipe:** for each slot, the chosen option and whether it came from the user or from auto. Also the last scaling choice. Remembered auto picks act as soft preferences, so the view does not jump around when new variants appear. User picks are restored as pins.
- **Across recipes (product preferences):** when a user pins an option whose product differs from the base product (e.g. base `śmietana 18%` → picked `śmietanka 30%`), the server increases the weight for the pair *from → to*. In other recipes these weights raise the rank of variants that make the same swap (rank criterion 2).
- Changes are saved automatically, debounced by about 1 s.

## 7. Product catalog

### 7.1 Shared and growing
There is **one global catalog** for recipe ingredients, fridge items and the shopping list (D-06). It is seeded with about 150 common Polish products. Any user can add a product. Duplicates are blocked by normalized name (lower-case, no diacritics, collapsed spaces), and the API returns the existing product instead.

### 7.2 Product fields
`name` (Polish), `category` (warzywa, owoce, mięso, ryby, nabiał, pieczywo, sypkie, przyprawy, oleje i octy, sosy, konserwy, mrożonki, napoje, słodycze, inne), `measureKind`, `displayClass`, `hasSizes`, `allowHalf`, `gramsPerMl?`, `gramsPerPiece?`, `defaultPackageAmount?` (e.g. 330 ml for cream), `pieceForms?`.

### 7.3 Admin
Admins can edit any product and **merge** duplicates. A merge re-points every reference (ingredients, variant operations, fridge items, shopping-list items, preferences) to the target product, then deletes the source. Regular users cannot edit a product after creating it. This avoids silently changing other people's recipes.

## 8. Fridges

### 8.1 Fridges and members
- A user can own or belong to **many fridges**. Each new user automatically gets a fridge named "Moja lodówka".
- Roles: **owner** (rename, delete, invite, remove members) and **member** (full edit of items and the shopping list, can leave).
- **Invites are links** (D-08). The owner creates a link that is valid for 7 days and can be used any number of times until it expires or is revoked. A logged-in user who opens it sees the fridge name and owner and taps **"Dołącz"** (join).
- An owner cannot leave their own fridge; they can delete it.

### 8.2 Items
- An item is a product, a **package amount** (in base units, e.g. 330 ml) and **remaining percent** (0–100). An optional note.
- The UI shows `Śmietanka 30% — 330 ml · zostało 70% (~230 ml)`, with the remaining amount formatted by §5.4.
- The percent is edited with a slider in steps of 5 %. For `count` products the UI edits whole pieces instead (e.g. 6 eggs → 4 left), and they are converted to percent.
- The same product can appear several times (several packages).
- An item that reaches 0 % is removed. Removals caused by a deduction can be undone (§9).
- Adding an item: search the catalog (or create a product), then the package amount (defaults to the product's `defaultPackageAmount`), then the percent (defaults to 100).

## 9. Deducting a cooked recipe from the fridge

At the bottom of the recipe view, the **"Odejmij z lodówki"** (deduct from fridge) button opens a dialog:
- a fridge picker (defaults to `defaultFridgeId`),
- one row per ingredient of the **currently resolved and scaled** recipe ("do smaku" rows are left out): the amount needed, what is available in that fridge, and a **switch**,
- the switch defaults to on when the product is in the fridge,
- **warnings**: **"Brak w lodówce"** (not in the fridge; switch disabled) and **"Za mało — jest ~120 ml"** (not enough),
- actions: **"Odejmij zaznaczone"** (deduct selected), **"Odejmij wszystko"** (deduct all available), and **"Dodaj brakujące do listy zakupów"** (add missing items to the shopping list; adds shortfalls and missing items, §10).

Server rules (D-10):
- The deduction happens in base units, against the product's packages in that fridge, **most-used package first** (lowest remaining percent).
- A shortfall deducts to 0, removes those items, and reports the shortfall.
- A deduction is recorded with the previous state of every touched item, and the recipe and variant **cook count** goes up by one.
- **Undo** is offered as a toast ("Odjęto 6 produktów · Cofnij"). It succeeds only if none of the touched items has changed since; otherwise the API answers 409 and the UI explains why.

## 10. Shopping list

- There is **one shopping list per fridge**, shared by its members, because buying refills that fridge (D-11).
- An item is a catalog product with an optional amount, or free text. Each item can be checked off.
- Adding a product that is already on the list (unchecked) **sums the amounts** instead of creating a duplicate.
- **"Przenieś kupione do lodówki"** (move bought items to the fridge): checked catalog items become new fridge items (the package is the amount or the product default, at 100 %) and are removed from the list.
- **"Usuń kupione"** removes checked items.
- **Exporting to Google Keep** (D-03): the official Keep API is not available to personal Gmail accounts, so:
  - **"Udostępnij"** (share) uses the **Web Share API** (the system share sheet on a phone, where Keep is offered). The text is a title line followed by one unchecked item per line.
  - **"Kopiuj"** (copy) puts the same text on the clipboard. Pasted into a Keep checklist note, each line becomes an item.
  - Format: `Lista zakupów — <fridge name>`, a blank line, then `Mleko — 1 l` style lines with no bullets.

## 11. Non-functional requirements

- **Mobile-first**, responsive from 360 px wide. Touch targets ≥ 44 px. The recipe view works one-handed (bottom sheets for option pickers, sticky action bar).
- **Accessibility**: semantic HTML, keyboard reachable, visible focus, WCAG AA contrast, `prefers-reduced-motion` respected.
- **Performance**: recipe view interactive in < 1.5 s on a mid-range phone with a cold cache. Variant matching runs on the client and responds in < 50 ms for recipes with 50 variants.
- **Photos**: JPEG, PNG or WebP, up to 10 MB. Stored re-encoded as WebP at most 1600 px plus a 400 px thumbnail. EXIF data is removed.
- **Privacy**: emails are never exposed through the API to other users.
- **Deployment**: runs locally first; ready for self-hosting with Docker Compose.

## 12. Out of scope for v1 (future candidates)
Servings-based scaling · expiry dates and "use soon" highlighting · direct Google Keep / Google Tasks sync · "what can I cook from my fridge" · ratings and comments · offline/PWA · languages other than Polish · moderating the product catalog beyond admin merge.

## 13. Decision log

| ID | Decision | Source |
|---|---|---|
| D-01 | Display name "Przepiśnik", code name `RecipesList` | assumption, easy to change |
| D-02 | ~~Public catalog for everything~~, superseded by D-21. Anyone who can see a recipe can add variants to it | user |
| D-03 | Shopping-list export to Keep = Web Share API plus clipboard (no Keep API for personal accounts) | user |
| D-04 | Auto-match ranks options (dims, never hides). "Create variant" saves the current mix | user |
| D-05 | Selection memory is stored per user on the server and cached on the client | user |
| D-06 | Global product catalog; any user adds, duplicates blocked by normalized name | user |
| D-07 | Fixed conversion constants (glass = 250 ml, etc.), no runtime online lookups | interpretation of "use online conversion" |
| D-08 | Fridge sharing via invite links; owner and member roles | user |
| D-09 | `szybkie` = prep + cook ≤ 30 min, computed automatically | assumption (brief said "<30min") |
| D-10 | A shortfall deducts to 0 and warns; most-used package first; undo supported | user |
| D-11 | Shopping list per fridge (shared household) | assumption |
| D-12 | The variant editor edits a full copy; the server computes the diff | assumption |
| D-13 | Liquids: user setting kitchen (glasses) or metric (ml) for 62.5–1000 ml; spoons/drops below and litres above in both | user + assumption on thresholds |
| D-14 | Piece sizes are shown as the best combination of small/medium/large (fewest pieces within ±10 %) | user |
| D-15 | JSON export = recipe + all variants + embedded images; import creates copies owned by the importer | user |
| D-16 | At factor 1, amounts show exactly as the author entered them | assumption |
| D-17 | Deleting a recipe that has other users' variants archives it instead (editing under D-25 does delete them, as the user explicitly asked) | assumption |
| D-18 | Login required for the whole app (no anonymous browsing) | assumption |
| D-19 | Fridge, shopping list and deduction "available" amounts always use metric (inventory format) | assumption |
| D-20 | Variants cannot reorder base rows in v1 | assumption |
| D-21 | Recipe and variant visibility: private / fridges / public. A variant is visible only if both it and its recipe are | user |
| D-22 | "fridges" = everyone sharing at least one fridge with the author, computed live | user |
| D-23 | New recipes, variants and imports default to private | assumption |
| D-24 | Narrowing a recipe's visibility is blocked while other users have variants of it | user (un-publishing) + assumption (fridges → private) |
| D-25 | Public recipes can't be edited directly. Editing a private or fridge-only recipe that has variants deletes all of them after a confirmation prompt | user |
| D-26 | Favourites (♥) for recipes and variants, shown in Moje przepisy → Ulubione, private to the user, and a ranking signal in auto-match | user + assumption (ranking) |
