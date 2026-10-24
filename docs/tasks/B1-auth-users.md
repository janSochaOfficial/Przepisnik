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

## Report

### Summary
- `IGoogleTokenValidator` + `GoogleTokenValidator` (`backend/src/RecipesList.Infrastructure/Auth/`) wrap `GoogleJsonWebSignature.ValidateAsync` with audience `Auth:Google:ClientId` (1 min clock tolerance). They return null for an invalid or expired token, a token for the wrong audience, or when no client id is configured. Registered in `AuthFeature.AddAuth`.
- `POST /auth/google` validates the token, upserts the user by `sub` (refreshing email, avatar and the admin flag) and signs in. An invalid token gets 401 `auth.invalid_token`. Rate-limit policy `auth` is on every `/auth/*` endpoint.
- Google login and dev-login share one creation path, `UserAccounts.SignInAsync`. It saves the user first, then calls B5's `FridgeService.CreateOwnedAsync`, which creates "Moja lodówka" and makes it the default because the user has none. If two first logins with the same `sub` run at once, the unique index catches it and the loser signs in as the existing user.
- New `Core/Users/NicknameRules`: trim, compose to NFC, then `^[\p{L}\p{Nd}_-]{3,30}\z`. Uniqueness is checked on the `NameNormalizer` form. `SetNicknameAsync` also maps a unique-index race to 409.
- New `Core/Users/UserService` plus `Api/Features/Users/UsersEndpoints` (mapped from `MapAuth`, because `Program.cs` is a hotspot) for:
  - `GET /me`
  - `PUT /me/nickname`, which re-issues the cookie so the onboarding claim updates
  - `GET /users/nickname-available`
  - `PUT /me/settings`, which checks the default fridge with B5's `FridgeAccess.RequireMemberAsync` (404 `fridge.not_found`)
  - `GET /me/product-preferences`
- Tests:
  - Unit tests: `tests/RecipesList.UnitTests/Users/NicknameRulesTests.cs`.
  - Integration tests: `tests/RecipesList.IntegrationTests/Auth/{GoogleLoginTests,GoogleTokenValidatorTests,FakeGoogleTokenValidator}.cs` and `Users/UserEndpointsTests.cs`.
  - They cover every acceptance item: user plus default fridge, no duplicates on a second or concurrent login, invalid token, onboarding gate on `/recipes`, length/characters/case/Polish letters, the `liquidUnits` round trip, a foreign fridge giving 404, no other user's email on `/fridges/{id}`, `/fridges` or `/invites/{token}`, 429 `rate_limited`, and contract schemas.
- `backend/http/auth.http` added.

### Deviations
- **Additive hook in B5's file:** `IFridgeService.CreateOwnedAsync(ownerId, name, ct)` was added to `backend/src/RecipesList.Core/Fridges/FridgeService.cs`. `CreateAsync` now delegates to it, with no change in behaviour. It was needed because the first login has no session yet, so `ICurrentUser` can't be used.
- **Admin flag needs a verified email:** `isAdmin` is granted only when Google reports `email_verified`. This is the conservative reading of `Admin:Emails`.
- **Blank nicknames:** `PUT /me/nickname` with a blank or whitespace nickname answers 400 `nickname.invalid` (API.md), not `validation.failed`. Dev-login's nickname lost its `[MaxLength(30)]` annotation, so an over-long one also answers `nickname.invalid`, as API.md lists.
- **Diacritics count as the same nickname:** uniqueness uses `NameNormalizer`, as the task says. So `Łucja` and `Lucja` collide, as well as `Ania` and `ania`. The rule is tested.
- **`defaultFridgeId: null` clears the default:** in `PUT /me/settings`, `null` (or a missing value, which the contract treats the same) clears the default fridge. The spec doesn't say which way; this is the more permissive reading.
- **`nickname-available` and the caller's own nickname:** it returns `true` for the caller's own nickname (in any case), because setting it would succeed. It returns `false` for a malformed one.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- Shopping-list owner: `ShoppingListTests.Complete_MoveToFridge_CreatesFullPackagesAndRemovesCheckedEntries` failed once with the two `createdFridgeItems` in reverse order, then passed on rerun. `ShoppingListService` orders by `CreatedAt` with no tie-breaker (lines ~97/221), so rows with equal timestamps come back in any order. Suggest adding `.ThenBy(i => i.Id)`.

### Follow-ups
- First login runs three `SaveChanges` without a shared transaction: user, fridge, default. If the fridge step fails, the user is left without a fridge. `IAppDbContext` doesn't expose transactions, so this was left as is.
- In a concurrent first login, the losing request may return `defaultFridgeId: null` if the winner hasn't created the fridge yet. The next `/me` shows it.
- The frontend (F3) can now drop any mocks for these endpoints. The contract is unchanged.
- The full gate needed reruns because of machine load: vitest timeouts in `NumberField`/`toast`, plus the shopping-list flake above. The third run was fully green.
