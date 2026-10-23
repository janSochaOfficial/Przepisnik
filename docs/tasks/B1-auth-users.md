# B1: Auth and users (backend)

**Wave 1 · depends on: T00**

## Goal
Google sign-in, the current-user endpoints, nickname onboarding, settings and product preferences (read side).

## Read first
PROJECT §2 · TECHNICAL §3.5, §3.6 · API §1.

## Skills
`dotnet-webapi`.

## Owns
`backend/src/*/Features/Auth/**`, `*/Features/Users/**`, `backend/src/RecipesList.Core/Users/**`, `backend/src/RecipesList.Infrastructure/Auth/**`, matching test folders, `backend/http/auth.http`.

## Touch only if needed
`User` entity/configuration (additive).

## Deliverables
- `IGoogleTokenValidator` (Infrastructure) wrapping `GoogleJsonWebSignature.ValidateAsync` with audience `Auth:Google:ClientId`. Tests use a fake.
- `POST /auth/google`: validate the token, upsert the user by `sub` (refresh email and avatar), set `IsAdmin` from `Admin:Emails`, then sign in. On first creation, create the fridge "Moja lodówka" with the user as owner and set `DefaultFridgeId`.
- `GET /auth/config`, `POST /auth/logout`, `GET /me`, `PUT /me/nickname` (rules from PROJECT §2; uniqueness on the normalized nickname; 409 `nickname.taken`), `GET /users/nickname-available`, `PUT /me/settings` (the default fridge must be one the user belongs to), `GET /me/product-preferences`.
- Make dev-login (from T00) share the same user-creation path, including the default fridge.
- Rate-limit policy `auth` applied.

## Acceptance (integration tests unless noted)
- A valid fake Google token creates the user plus the default fridge; a second login doesn't duplicate either.
- An invalid token → 401 `auth.invalid_token`.
- A user without a nickname gets 403 `user.onboarding_required` on `/recipes`, but `/me` works.
- Nickname validation: length, characters, case-insensitive uniqueness (`Ania` vs `ania`), Polish letters allowed.
- Settings: `liquidUnits` round-trips; a foreign fridge id → 404.
- No response for one user ever contains another user's email (asserted on `/fridges/{id}` members once B5 is merged; add the test here with a `Skip` reason if B5 isn't merged yet).
- Unit tests for nickname validation rules.
- Responses validate against the contract.
