# F3: Login, onboarding, profile, invite acceptance (frontend)

**Wave 2 · depends on: F0 merged (and T00)**

## Goal
Everything that wraps the app: session state, login, nickname onboarding, profile and settings, and the invite landing page.

## Read first
PROJECT §2, §3, §8.1 · TECHNICAL §4 · API §1, §6 (invites).

## Skills
`svelte-core-bestpractices`, `svelte-code-writer`, `webapp-testing`.

## Owns
`frontend/src/lib/state/session.svelte.ts`, `frontend/src/routes/logowanie/**`, `routes/witaj/**`, `routes/profil/**`, `routes/zaproszenie/**`, `src/lib/i18n/pl/{auth,profile}.ts`, `tests/integration/mocks/{auth,profile}.ts`, and tests.

## Touch only if needed
The root `+layout.ts` guard from T00.

## Deliverables
- A `Session` class (`$state`) provided via context: `me`, `settings`, `refresh()`, `logout()`, `updateSettings()`. The liquid-unit setting is read by the formatting call sites.
- `/logowanie`: the Google Identity Services button (script loaded on that page only; `GET /auth/config` provides the client id) → `POST /auth/google`. When `devLoginEnabled` and there is no client id, show the dev-login form. After login, go to `/witaj` if there is no nickname, otherwise to the originally requested URL.
- `/witaj`: a nickname form with a live availability check (debounced `nickname-available`) and the validation rules from PROJECT §2.
- `/profil`: nickname (editable), the liquid units toggle (`Szklanki i łyżki` / `Mililitry`) with a live example (`¾ szklanki` ↔ `190 ml`) using F1's `formatBaseAmount` (stub-safe: if F1 isn't merged, show the example text from i18n), the default fridge select, "Importuj przepis" (a link to F4's import entry point, `/przepisy?import=1`), and log out.
- `/zaproszenie/[token]`: the preview (fridge name, owner) → **Dołącz** → redirect to `/lodowka/[id]`. If the user is not logged in, go through login and return here. Expired → a friendly empty state.

## Acceptance (Playwright with mocks, both viewports)
- First login → onboarding → catalog. A taken nickname shows an inline error.
- Changing the liquid units updates the example immediately and persists (PUT asserted).
- Invite accepted while logged out → login → back to the invite → joined.
- 401 from any API call → redirect to `/logowanie?next=…`.
- Component tests for the nickname field validation.

## Report

### Summary
- `Session` class (`lib/state/session.svelte.ts`, context via `provideSession`/`useSession`, created in the root layout): `me`, `settings`, `liquidUnits`, `refresh()`, `logout()`, `updateSettings()` (optimistic with rollback, sends both fields so `defaultFridgeId: null` clears), `setNickname()`. Mutations also `invalidate('app:me')`, so call sites reading `page.data.me` (`liquidUnitsOf`) stay current.
- `/logowanie`: F0 components; GIS script loaded on this page only (`routes/logowanie/google.ts`); dev-login form when `devLoginEnabled` and no client id. After login: `/witaj?next=…` without nickname, else `next`.
- `/witaj`: `NicknameField` (client rules mirrored from `NicknameRules`, debounced `nickname-available`, own name counts as free, server 409 shown inline); then `next`.
- `/profil`: nickname edit, liquid-units chips with live example via `formatBaseAmount` (190 ml ↔ ¾ szklanki), default fridge select (with "Brak"), link to `/przepisy?import=1`, log out.
- `/zaproszenie/[token]`: F6 join flow kept; expired/revoked now an `EmptyState`; login round-trip works through the layout guard (tested, including a brand-new user passing onboarding).
- Legacy colour aliases removed from `app.css`; only the old login page used them. Toaster takes `tabBar` (false on `/logowanie`, `/witaj`). Unused `PagePlaceholder.svelte` deleted.
- Tests: vitest (nickname rules, `NicknameField`, `safeNext`, example), Playwright `auth-profile.spec.ts` on both viewports incl. axe in light/dark.

### Deviations
- No `next` is added when the layout guard sends a signed-in user without a nickname to `/witaj` (keeps the existing smoke test); `next` is carried only on the login to onboarding path.
- No redirect away from `/witaj` for users who already have a nickname (avoids a race with the post-save navigation).
- `NicknameField` is client-only validated for length in UTF-16 units, as the backend regex does.
- Autofixer still prints advisory suggestions for the debounced fetch in an `$effect` and for `bind:this` on the GIS button container (both required); no issues.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- F6: the invite page strings for the expired state live in `t.auth.invite`; the rest remain in `t.fridge.join`.

### Follow-ups
- Google sign-in could not be exercised against the real GIS script (no client id); only the dev-login path is tested.
