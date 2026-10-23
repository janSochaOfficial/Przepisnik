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
