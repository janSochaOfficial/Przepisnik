# Decisions made by agents

Everything in this file was decided by an AI agent (the planning session, the coordinator, or a task agent), not by the user.
It collects them in one place so they can be reviewed, and it keeps the task reports' content after `docs/tasks/` was removed (they are still in git history: `git show 1200bc8:docs/tasks/<ID>-<slug>.md`).

To change any of these, record a new decision in [PROJECT.md §13](../PROJECT.md) (functional) or edit [TECHNICAL.md](../TECHNICAL.md) (technical), and it wins over this file.

**Decided by the user, for contrast** (not repeated below): the stack in the brief (SvelteKit + TypeScript, C# .NET + EF Core, PostgreSQL, unit and integration tests on both sides); the decision-log entries marked `user`: D-02 to D-06, D-08, D-10, D-14, D-15, D-21, D-22, D-25, D-27, D-28, and the user part of D-13, D-24 and D-26; at most 2 agents at once.

---

## 1. Worth reviewing first

These have the most visible effect on how the app behaves. Each one links to its full entry below.

| # | Decision | Where |
|---|---|---|
| 1 | The whole app needs a login; nothing can be browsed anonymously | D-18, §2 |
| 2 | New recipes, variants and imports are **private** by default | D-23, §2 |
| 3 | `szybkie` means prep + cook **≤ 30** min (the brief said "< 30 min") | D-09, §2 |
| 4 | One shopping list **per fridge**, not per user | D-11, §2 |
| 5 | Deleting a recipe that other people have variants of **archives** it instead | D-17, §2 |
| 6 | Variants cannot reorder the base recipe's rows | D-20, §2 |
| 7 | Nicknames that differ only in diacritics are the same (`Łucja` = `Lucja`) | D-29, §2 |
| 8 | No new variants on an archived recipe (the UI hides the action, the API answers 404) | §5.4, §5.10 |
| 9 | Visual direction "Bolesławiec" (cobalt on stoneware grey), fonts Literata + Atkinson Hyperlegible Next | §4 |
| 10 | Fridge and shopping amounts are typed in g / ml / szt., not in kg or l; a shopping amount can't be cleared once set | §5.6, §5.10 |
| 11 | The conversion constants other than the glass: tablespoon 15 ml, teaspoon 5 ml, drop 0.05 ml, pinch 0.3 ml | §3 |
| 12 | Production login cookie works over plain http only on `localhost`; any other host needs HTTPS | §6 |

---

## 2. Functional decisions in the PROJECT.md decision log

Entries whose source is `assumption` or `interpretation`.

| ID | Decision | Why the agent chose it |
|---|---|---|
| D-01 | Display name "Przepiśnik", code name `RecipesList` | Placeholder; easy to change |
| D-07 | Fixed conversion constants, no online lookups at runtime | Interpretation of "use online conversion": the values were looked up once and fixed, so results are stable and testable |
| D-09 | `szybkie` = prep + cook ≤ 30 min, computed automatically, a missing time counts as 0 | The brief said "<30min"; a recipe of exactly 30 minutes was counted as quick |
| D-11 | Shopping list per fridge (shared by the household) | Buying refills a fridge, so the people sharing it share the list |
| D-12 | The variant editor edits a full copy of the recipe; the server computes the diff | Users never see operations; one diff algorithm on the server |
| D-13 | Thresholds for liquids: glasses or ml between 62.5 and 1000 ml; spoons and drops below; litres above | The user chose the kitchen/metric setting; the agent chose the boundaries |
| D-16 | At scale factor 1, amounts show exactly as the author typed them | Rounding rules would otherwise rewrite the author's "1 łyżka" |
| D-17 | Deleting a recipe that has other users' variants archives it | Deleting would destroy other people's work |
| D-18 | Login required for the whole app | Visibility rules need a known viewer; simplest safe default |
| D-19 | Fridge, shopping list and the deduction dialog's "available" column always use metric | Packages are bought in metric units |
| D-20 | Variants can't reorder base rows in v1 | Keeps the diff model (add/modify/remove) simple |
| D-23 | New recipes, variants and imports are private | Conservative privacy default |
| D-24 (part) | Narrowing fridges → private is blocked too while others have variants | Extends the user's "can't un-publish" rule to the fridge level |
| D-26 (part) | Favourites rank higher in variant auto-match | Gives ♥ a use beyond the list |
| D-29 | Nickname uniqueness ignores diacritics and case | Prevents look-alike nicknames (coordinator) |

---

## 3. Spec details the planning agent filled in

The brief didn't give these values; the planning agent wrote them into PROJECT.md.

- **Users:** nickname 3–30 characters, letters/digits/`_`/`-`, Polish letters allowed. Sessions last 30 days and slide. Admin is a config flag (`Admin__Emails`) that lets a user edit and merge catalog products. Regular users can't edit a product after creating it.
- **Recipe limits:** title ≤ 150 characters, description ≤ 2000. Photos: JPEG/PNG/WebP up to 10 MB, re-encoded as WebP at most 1600 px plus a 400 px thumbnail, EXIF removed. Import file ≤ 25 MB.
- **Units:** tablespoon 15 ml, teaspoon 5 ml, drop 0.05 ml, pinch 0.3 ml (the glass at 250 ml and the 0.75/1/1.3 sizes came from the brief). Spices are volume products.
- **Display rounding after scaling** (PROJECT §5.4): weight steps 1/5/10 g, kg in 0.05 steps above 1000 g; glasses to the nearest ¼; spoons to the nearest ¼ teaspoon, split greedily into tablespoons + teaspoons; spices never in glasses or ml and pinches below 0.625 ml; pieces as the fewest small/medium/large pieces within ±10 %, `¼`/`½ średniej` below 0.625; a `~` prefix when the shown value is more than 5 % off.
- **Polish plurals:** four forms per unit (one / few / many / fraction).
- **Variant auto-match:** two amounts are the same option when within 1 %. Ranking order: remembered picks, favourites, product-preference score, cook count, base recipe first, then older variants. Product preferences learn "from → to" product swaps across recipes. Selection memory saves after about 1 s.
- **Catalog:** about 150 seeded products (S1 delivered 249 products, 35 tags, 50 utensils), 15 categories, duplicates blocked by normalized name. Piece weights and densities in the seed data are rough estimates.
- **Fridges:** every new user gets "Moja lodówka". Invite links are valid for 7 days and can be used any number of times. An owner can't leave their own fridge. The percent slider moves in 5 % steps; `count` products are edited as whole pieces.
- **Deduction:** most-used package first (lowest remaining percent); undo only while no touched item has changed since.
- **Shopping list:** adding a product that's already on the list sums the amounts. The share/copy text format is `Lista zakupów — <fridge>` and `Mleko — 1 l` lines.
- **Non-functional targets:** from 360 px wide, touch targets ≥ 44 px, WCAG AA; recipe view interactive in < 1.5 s; matching < 50 ms for 50 variants; self-hosting with Docker Compose.

---

## 4. Technical and design choices

From TECHNICAL.md, the planning session, and F0. The brief fixed only the main frameworks and the database.

- **Versions:** .NET 10 (LTS) / C# 14, EF Core 10, PostgreSQL 18 with `pg_trgm` and `unaccent`, Node 24 LTS, SvelteKit 2 + Svelte 5 (runes only), TypeScript 5.9. T00 pinned Kit 2 / TS 5.9 instead of the newer Kit 3 / TS 7.
- **Backend:** Minimal APIs (no controllers, no Swashbuckle; the built-in `AddOpenApi`). Three projects: Api / Core / Infrastructure, with no repository layer. T00 let Core reference the base EF Core package, because the fixed `IAppDbContext` exposes `DbSet<T>`, although TECHNICAL said "no EF in Core".
- **Data:** Guid v7 ids; snake_case names; ingredients and steps stored relationally, step blocks and variant operations as jsonb; optimistic concurrency on Postgres `xmin` (409 `concurrency.conflict`); trigram search indexes. B8 moved cook counts into their own tables (`recipe_cook_counts`, `variant_cook_counts`) to implement the user's D-28.
- **Auth:** a Google ID token is exchanged for a cookie session (`__Host-rl_session`, `rl_session` in dev over http). CSRF protection: same origin + `SameSite=Lax` + a required `X-Requested-With: fetch` header. Dev-login exists only in Development/Testing.
- **Images:** SkiaSharp; files on local disk.
- **Frontend:** client-only SPA; Tailwind CSS 4 with design tokens; `bits-ui` for dialogs, sheets and comboboxes; types generated from `openapi.yaml` (`openapi-typescript` + `openapi-fetch`). Every Polish string lives in `src/lib/i18n/pl/`.
- **Tests:** xUnit v3, Shouldly, NSubstitute, Testcontainers, Respawn; Vitest + Testing Library; Playwright with a mocked API, plus an E2E suite on the compose stack. Shared JSON test vectors keep the backend and frontend unit logic identical. T00 runs the tests on **VSTest instead of Microsoft.Testing.Platform**, because MTP on the .NET 10 SDK can't run `dotnet test backend` from the repository root.
- **Deployment:** Docker Compose with Postgres, the API and Caddy 2 (static files + reverse proxy) on port 8080. Data-protection keys are kept in a volume, so upgrades don't sign everyone out.
- **Process:** waves of parallel task agents in git worktrees, per-agent ports and databases, a single quality gate `scripts/verify.sh`, and no migrations in feature tasks.
- **Visual design (F0):** direction **"Bolesławiec"**: a cool stoneware-grey ground, cobalt as the only strong colour, a little green, dot-and-ring (_oczko_) motifs. Literata (titles) and Atkinson Hyperlegible Next (body), self-hosted via `@fontsource`. Documented in [frontend/DESIGN.md](../frontend/DESIGN.md), with a live showcase at `/ui` in dev.

---

## 5. Behaviour decided while implementing

The spec was silent on these points, so each agent picked a behaviour and the coordinator accepted it.

### 5.1 Users and login (B1, F3)
- The admin flag is granted only when Google reports the email as **verified**.
- A blank or too-long nickname → 400 `nickname.invalid`.
- `PUT /me/settings` with `defaultFridgeId: null` clears the default fridge.
- `nickname-available` returns true for your own current nickname.
- A first login creates the user, the fridge and the default in one transaction (T90 hardening).
- The onboarding guard sends a user without a nickname to `/witaj` without a `next` parameter. `/witaj` doesn't redirect users who already have a nickname.

### 5.2 Catalog (B2, S1)
- An admin can't change the `measureKind` of a product already in use: 400 `product.invalid_combination`.
- Merging a product into itself: 400.
- Tag and utensil names are limited to **60** characters (the column size). Fridge names allow 80.
- A merge also rewrites remembered selections and stored deductions that point at the source product.
- Seed names start lower-case. Every `count` product has a piece weight. Some mass products (mustard, honey, jam) have a density so that spoon measures work.

### 5.3 Recipes and images (B3)
- Editing an archived recipe with "delete variants" un-archives it. Since D-27, archived recipes are read-only, so this path is no longer reachable.
- The `szybkie` tag row is created on demand if the seed is missing.
- Recipe search is a substring match on the normalized title, not a similarity ranking.
- Unknown or someone else's image ids → 400 `image.not_owned`.

### 5.4 Variants (B4, F2, F5)
- Edits the v1 operations can't express are **rejected** with 400 rather than silently dropped: reordering base rows, a base row sent twice, moving a step to the other section, clearing a time the base sets.
- A variant that adds the same product twice lands in one slot with its first row.
- The 1 % rule is inclusive and measured against the first amount seen.
- Variant names are unique per recipe and author, after trimming, and **case-sensitive**.
- Access: no new variants on an archived recipe (404). A variant whose base recipe you can no longer see can't be edited (404), only deleted.
- Remembered picks for options that no longer exist are dropped silently.
- A variant's step photo must be yours, already in the base recipe, or in a visible variant, because "create from selection" copies other people's steps.
- Added ingredients carry `afterSlotKey` (the base row they follow), so the view can place them in position. This was a contract addition.
- **Custom mix** ("Własna kombinacja"): nothing is dimmed (every option would be), options are ordered by support count, and the steps come from the closest variant.
- In a selection, a base ingredient picked as "brak" stays visible, struck through ("pominięty w tym wyborze"), so it can be restored. An added slot set to "brak" is hidden.
- Markers: your own pins show "Twój wybór" with a pin icon; auto picks that differ from the base show "Auto".
- `?wariant=` in the URL is ignored when a saved selection exists.
- In the variant editor, a 409 conflict reloads the version, keeps your draft and asks you to save again.

### 5.5 Favourites (B7)
- A favourite variant is listed only while its base recipe is visible too.
- Favourites of archived recipes aren't listed (they're hidden, not deleted).

### 5.6 Fridges, deductions and the shopping list (B5, F6)
- Removing someone who isn't a member: 204, no-op.
- Adding an item at 0 % is rejected. Setting an existing item to 0 % deletes it.
- A deduction where nothing at all can be deducted → `deduction.empty`, and nothing is recorded. Missing products in an otherwise valid deduction come back as a full shortfall.
- **Archived recipes can still be cooked** (deducted); only their cook count changes.
- "Move bought items to the fridge": a checked product with no amount and no default package size **stays on the list**.
- The default fridge is set on create or join only if you have none. When you lose it, it falls back to a fridge you own, then the one you joined earliest.
- Sort orders: fridges oldest first; members owner first, then by join time; items by category, name, then most-used package; shopping list unchecked first, then by category and name; invites newest first.
- Shopping PATCH treats `null` as "unchanged"; an amount is cleared with `clearAmount` (U2). Unchecking doesn't merge an entry into another one.
- Amounts in the fridge form are typed in **base units** (g / ml / szt.); the shopping dialogs take any valid unit (U2).
- Editing a fridge item saves with a "Zapisz zmiany" button, not when the slider is released.
- Undo of an item delete re-creates the item under a new id.
- **U2 (shopping units):** metric quantities on the list show a decimal comma (`1,5 l`, PROJECT §10) instead of U1's `1 ½ l`; other units use U1's as-entered format. The dialogs have no piece-size selector (an existing size is kept when converting). An item with only a base amount opens in its base unit and is saved with it. Unit conversion for the dialogs is frontend-only; the merge uses `IQuantityConverter`.
- Concurrent accepts of the same invite are idempotent; a concurrent undo of a deduction → 409 (T90).

### 5.7 Import and export (B6, I1)
- Export removes the "originally by @nick" footer and reports that nickname as `authorNickname`, so export → import → export round-trips and the original author survives chains of imports. Variants carry the footer too.
- A description that goes over 2000 characters with the footer is **cut with "…"** instead of rejecting the file.
- Clashing variant names get " (2)", " (3)".
- Photos are re-encoded on import, so their bytes differ from the original.
- **Import as variants (I1):**
  - Besides an empty diff, a candidate is skipped when its result equals the target, a visible existing variant of it, or a variant created earlier in the same import.
  - A file photo byte-identical to a step photo of the target (or a visible variant) reuses that image; new photos are stored only when a created variant uses them; the cover is ignored.
  - A time missing from the file keeps the target's time (a variant can't clear a base time).
  - Section start and end count as step anchors, so steps with no common text are paired by position; LCS ties keep the earliest target row.
  - The file's recipe is validated with `RecipeInputValidator`; errors → 400 `import.invalid_format`. A 201 has `Location: /api/v1/variants/{first id}`.
  - A file without a usable title (1–150 chars) skips matching. A success toast follows a variant import.
- **Format help page (I2):** field limits not in the export schema are taken from `RecipeWrite`; a Vitest test checks that the example validates and that the recipe and variant tables list every field of the contract.
- System tags (`szybkie`) aren't exported. Op key names are fixed (`key`, `targetKey`, `afterKey`, `productKey`, `imageKey`).

### 5.8 Quantities library (F1)
- An unusable scaling anchor (0, negative, not finite) gives factor 1 instead of an error.
- Rounded weights and ml are at least 1. A value that rounds to 1000 g is shown as `1 kg`.
- Quarters are shown as ¼ ½ ¾ glyphs; other values use a decimal comma with at most 2 digits.
- A quantity that can't be converted to a base amount is scaled as typed and never shown with `~`.

### 5.9 Recipe catalog and editor (F4)
- The search box and tag chips appear only on Wszystkie and Utworzone (the other lists have no filters in the API).
- Import opens a dialog with a file field, because browsers don't allow opening a file picker without a click.
- The inline "new product" form has no piece-form (plural) fields.
- "Zmień widoczność" and "Usuń przepis" are also on the edit page.
- Drag-and-drop of ingredient rows on desktop uses native HTML5 drag events.

### 5.10 Recipe view (F5)
- On archived recipes, "Edytuj", "Zmień widoczność", "Nowy wariant" and "Utwórz nowy wariant…" are hidden, and `/warianty/nowy` explains why.
- The recipe menu is a bottom sheet (there's no dropdown-menu component).
- Option amounts in the picker sheet are shown scaled by the current factor.
- **Warianty tab and opening a variant (R1):**
  - `?wariant=` is applied by `SelectionState.openVariant`, also on client navigation; an unknown id changes nothing. A kept scaling anchor needs an amount in the opened variant.
  - "Przepis bazowy" pins every slot to the base (clearing would let a favourite variant win the auto-match), so "Wyczyść wybór" shows afterwards. It has no ♥ of its own: the recipe's is by the title (`Page`'s additive `titleAside`).
  - Visibility labels come from a lazy `GET /recipes/{id}/variants` (the matrix has no visibility).
  - The two hearts have distinct accessible names ("… przepis …" / "… aktywny wariant …").
- **Servings (R2):**
  - Clearing servings in a variant means "inherit" (unlike times, where clearing a base time is rejected).
  - The servings line under an ingredient scaling uses the §5.4 `~` rule: ×0.6 on 4 → `2 ½ porcji` without `~` (4.2 % < 5 %); the task file's vector was wrong.
  - `ScalingState` is a `kind` union; an unknown `kind` → 400 `request.invalid`. A servings choice is kept when variants change or are deleted.
  - Typing more than 100 servings in an editor empties the field (the shared `NumberField` behaviour, as for times), so the form saves without servings.

---

## 6. Security and deployment (T90)

- The CSP is set by SvelteKit (`kit.csp`, hash mode) as a `<meta>` tag; Caddy adds `frame-ancestors 'none'`. It allows Google Identity Services and Google avatar images. **It hasn't been tested with the real Google script** (there's no client id on this machine).
- The production cookie is `Secure`: over plain http it works only on `localhost`. Any other host needs HTTPS (see [deploy/README.md](../deploy/README.md)).
- `POSTGRES_PASSWORD` defaults to `recipes`; set it in `deploy/.env` on a real host.
- The web container has no health check; compose starts it after the API is healthy.
- A dev-only test page, `/lodowka/harness-odejmowanie`, was kept for four integration tests. Like `/ui`, it is excluded from production builds.
- Every integration test response is checked for leaked emails (`EmailLeakGuard`).
- Npgsql's Kerberos probe is disabled in the container connection string (`GSS Encryption Mode=Disable`).

---

## 7. Coordinator decisions (merging and process)

- Added the migrations the feature tasks reported: `B5_FridgeOwnerNotUnique` (the initial model wrongly made fridge owners unique, so nobody could create a second fridge) and `B8_CookCountTables` (copies the counts, then drops the old columns; Down copies them back).
- Accepted the VSTest test runner (§4) and Core's EF Core reference.
- Fixed contract drift on merge: tags/utensils use a 60-character name type, and the "measureKind in use" error is documented.
- Fixed the order of items created by "move to fridge" (it was random, because the batch shares one timestamp).
- Recovered B6's branch after an interrupted commit (6 empty git objects quarantined, branch reset, work recommitted; `git fsck` clean).
- Added the migrations `U1_CustomUnits`, `P1_ProductIsVerified`, `U2_ShoppingItemQuantity` (v1.1) and `R2_Servings` (two nullable columns with 1–100 checks).
- After R2 the coordinator added `servings` / `servingsOverride` to the I2 help page (I2 owned it and had already merged).
- B8's migration id was set to sort after B5's, because the system clock had been about 1.7 h fast.

---

## 8. Known open issues (left unfixed on purpose)

- A concurrent import that creates the same new product, tag or utensil → 500 (needs a savepoint or `ON CONFLICT`).
- Export when an image file is missing from storage → 500.
- Old deductions are never pruned; they only serve undo.
- Recomputing product preferences reads all of a user's selections on every save.
- Real Google sign-in is confirmed only on `npm run dev` (G1); the Docker stack (:8080) is untried.
- Small duplications: recipe and variant DTO shapes; the client and F2 both order added slots.
- Playwright tests can time out when the machine is under heavy load (rerunning passes). Since 2026-10-25, `PLAYWRIGHT_WORKERS=2` is used while two agents run; I2 still saw 4 mobile `recipe-view` timeouts once.
- `?wariant=` saves the selection even when the picks equal the remembered ones (R1). Recipe cards don't show servings (R2).
