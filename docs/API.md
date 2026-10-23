# API contract v1

This is the human-readable source of truth. Task T00 transcribes it into `docs/api/openapi.yaml` (OpenAPI 3.1). From then on both files change together.
Conventions are in [TECHNICAL.md §3.3, §5](../TECHNICAL.md). Behaviour is in [PROJECT.md](../PROJECT.md).

- Base path: `/api/v1`. Every endpoint requires an authenticated session except those marked **public**.
- Every mutating request must send the header `X-Requested-With: fetch`.
- Types below use TypeScript notation. `Uuid` and `DateTime` (ISO 8601 UTC) are strings. `?` means the property may be `null` (properties are always present in responses).
- `Version` is a `number` (uint32 from Postgres `xmin`).

## 0. Common types

```ts
type Paged<T> = { items: T[]; page: number; pageSize: number; totalCount: number };

type MeasureKind = 'mass' | 'volume' | 'count';
type DisplayClass = 'weight' | 'liquid' | 'spice' | 'piece';
type ProductCategory = 'vegetables' | 'fruits' | 'meat' | 'fish' | 'dairy' | 'bakery' | 'dryGoods'
  | 'spices' | 'oilsVinegars' | 'sauces' | 'canned' | 'frozen' | 'drinks' | 'sweets' | 'other';
type Unit = 'g' | 'kg' | 'ml' | 'l' | 'glass' | 'tbsp' | 'tsp' | 'drop' | 'pinch' | 'piece';
type PieceSize = 'small' | 'medium' | 'large';
type LiquidUnits = 'kitchen' | 'metric';
type StepSection = 'prep' | 'instructions';
type FridgeRole = 'owner' | 'member';
type Visibility = 'private' | 'fridges' | 'public';   // PROJECT §4.4; resources the caller can't see answer 404

type PluralForms = { one: string; few: string; many: string; fraction: string };

type ProductRef = {               // embedded wherever a product is referenced
  id: Uuid; name: string; category: ProductCategory;
  measureKind: MeasureKind; displayClass: DisplayClass;
  hasSizes: boolean; allowHalf: boolean;
  gramsPerMl?: number; gramsPerPiece?: number;
  defaultPackageAmount?: number;  // base units
  pieceForms?: PluralForms;
};

type Quantity = { value: number; unit: Unit; size?: PieceSize };  // as entered by the author; size only for unit 'piece' + hasSizes

type ContentBlock =
  | { type: 'text'; text: string }                         // plain text, \n preserved, ≤ 5000 chars
  | { type: 'image'; imageId: Uuid; caption?: string };    // caption ≤ 300 chars

type UserRef = { id: Uuid; nickname: string };
type TagRef = { id: Uuid; name: string; isSystem: boolean };
type UtensilRef = { id: Uuid; name: string };
```

## 1. Auth and current user

| Method | Path | Body | Response | Errors |
|---|---|---|---|---|
| POST | `/auth/google` **public** | `{ idToken: string }` | `200 Me` + session cookie | 401 `auth.invalid_token` |
| POST | `/auth/dev-login` **public, Development/Testing only** | `{ email: string; nickname?: string }` | `200 Me` + cookie (sets the nickname if the user has none) | 404 outside dev/test, 400 `nickname.invalid`, 409 `nickname.taken` |
| POST | `/auth/logout` | — | `204` | |
| GET | `/me` | — | `200 Me` | 401 |
| PUT | `/me/nickname` | `{ nickname: string }` | `200 Me` | 409 `nickname.taken`, 400 `nickname.invalid` |
| GET | `/users/nickname-available?nickname=` | — | `200 { available: boolean }` | |
| PUT | `/me/settings` | `MeSettings` | `200 Me` | 404 `fridge.not_found` (defaultFridgeId must be one of my fridges) |
| GET | `/me/product-preferences` | — | `200 ProductPreference[]` | |
| GET | `/auth/config` **public** | — | `200 { googleClientId?: string; devLoginEnabled: boolean }` | |

```ts
type MeSettings = { liquidUnits: LiquidUnits; defaultFridgeId?: Uuid };
type Me = {
  id: Uuid; email: string; nickname?: string;   // nickname null ⇒ onboarding required
  avatarUrl?: string; isAdmin: boolean; settings: MeSettings;
};
type ProductPreference = { fromProductId: Uuid; toProductId: Uuid; weight: number };
```
When a user is created, a fridge named "Moja lodówka" is created too and set as `defaultFridgeId`.
Every endpoint other than `/auth/*`, `/me*` and `/users/nickname-available` returns `403 user.onboarding_required` while `nickname` is null.

## 2. Catalog (products, tags, utensils)

| Method | Path | Body / query | Response | Errors |
|---|---|---|---|---|
| GET | `/products` | `q?` (trigram + prefix on normalized name), `category?`, `limit` (≤ 50, default 20) | `200 ProductRef[]` | |
| GET | `/products/{id}` | — | `200 ProductRef` | 404 `product.not_found` |
| POST | `/products` | `ProductWrite` | `201 ProductRef` | 409 `product.duplicate` + extension `existingId` |
| PUT | `/admin/products/{id}` | `ProductWrite` | `200 ProductRef` | 403 `admin.required`, 409 `product.duplicate` |
| POST | `/admin/products/{id}/merge` | `{ targetId: Uuid }` | `204` | 403, 404, 400 `product.merge_incompatible` (measureKind differs) |
| GET | `/tags` | `q?`, `limit` | `200 TagRef[]` | |
| POST | `/tags` | `{ name: string }` | `201 TagRef` (or `200` with the existing tag on a normalized match) | 400 `tag.reserved` (`szybkie`) |
| GET | `/utensils` | `q?`, `limit` | `200 UtensilRef[]` | |
| POST | `/utensils` | `{ name: string }` | `201` / `200` existing | |

```ts
type ProductWrite = {
  name: string;                    // 2–80 chars
  category: ProductCategory; measureKind: MeasureKind; displayClass: DisplayClass;
  hasSizes: boolean; allowHalf: boolean;
  gramsPerMl?: number; gramsPerPiece?: number; defaultPackageAmount?: number;
  pieceForms?: PluralForms;
};
```
Validation: `displayClass` must fit `measureKind` (`weight`→`mass`; `liquid`/`spice`→`volume`; `piece`→`count`); `hasSizes` and `allowHalf` only for `count` → otherwise 400 `product.invalid_combination`.

## 3. Images

| Method | Path | Body | Response | Errors |
|---|---|---|---|---|
| POST | `/images` | `multipart/form-data` field `file` | `201 ImageInfo` | 400 `image.invalid_type`, 413 `image.too_large` |
| GET | `/images/{id}` | `?size=full\|thumb` (default `full`) | `200 image/webp` | 400 (`validation.failed`, invalid `size`), 404 |

```ts
type ImageInfo = { id: Uuid; url: string; thumbnailUrl: string; width: number; height: number };
// url = "/api/v1/images/{id}", thumbnailUrl = "/api/v1/images/{id}?size=thumb"
```
An image can only be referenced in recipes and variants by the user who uploaded it, or when it is already referenced by the recipe being edited or its base (400 `image.not_owned`).

## 4. Recipes

| Method | Path | Body / query | Response | Errors |
|---|---|---|---|---|
| GET | `/recipes` | `q?`, `tags?` (comma-separated tag ids, AND), `author?` (`me` or user id), `visibility?`, `page`, `pageSize`, `sort` (`newest`\|`title`) | `200 Paged<RecipeSummary>` (only recipes visible to the caller) | |
| GET | `/recipes/{id}` | — | `200 RecipeDetail` | 404 `recipe.not_found` (also when not visible) |
| POST | `/recipes` | `RecipeWrite & { visibility: Visibility }` | `201 RecipeDetail` | 400 validation, 400 `quantity.unit_incompatible`, 400 `image.not_owned` |
| PUT | `/recipes/{id}` | `RecipeWrite & { version: Version; deleteVariants: boolean }` | `200 RecipeDetail` | 403 `recipe.not_author`, 409 `recipe.archived`, 409 `recipe.public_locked`, 409 `recipe.has_variants` (when variants exist and `deleteVariants` is false; extensions `variantCount`, `foreignVariantCount`), 409 `concurrency.conflict` |
| PUT | `/recipes/{id}/visibility` | `{ visibility: Visibility }` | `200 RecipeDetail` | 403 `recipe.not_author`, 409 `recipe.archived`, 409 `recipe.visibility_locked` (narrowing while other users have variants; extension `foreignVariantCount`) |
| PUT | `/recipes/{id}/favorite` | — | `204` (idempotent) | 404 |
| DELETE | `/recipes/{id}/favorite` | — | `204` (idempotent) | |
| DELETE | `/recipes/{id}` | — | `204` (deleted or archived, see PROJECT §4.4) | 403 |
| GET | `/recipes/{id}/export` | — | `200 RecipeExport` (variants limited to those visible to the caller) with `Content-Disposition: attachment; filename="<slug>.json"` | 404 |
| POST | `/recipes/import` | `RecipeExport` (≤ 25 MB) | `201 RecipeDetail` (recipe and variants are `private`) | 400 `import.invalid_format`, 400 `import.unsupported_version` |

Archived recipes are excluded from `GET /recipes`. `GET /recipes/{id}` still returns them, with `isArchived: true` and `canEdit: false`, to the author and to authors of their variants. They are read-only: `PUT /recipes/{id}` and `PUT /recipes/{id}/visibility` return 409 `recipe.archived` (D-27).
**Direct edit rules (PROJECT §4.5):** `PUT /recipes/{id}` on a `public` recipe → 409 `recipe.public_locked`. If variants exist, the client first gets 409 `recipe.has_variants`, shows the confirmation, then retries with `deleteVariants: true`. The server then deletes all variants (and their favourites and remembered picks) in the same transaction as the update.

```ts
type RecipeSummary = {
  id: Uuid; title: string; author: UserRef; coverImage?: ImageInfo;
  visibility: Visibility; isFavorite: boolean;
  tags: TagRef[]; prepMinutes?: number; cookMinutes?: number;
  variantCount: number;           // variants visible to the caller
  createdAt: DateTime; updatedAt: DateTime;
};

type Ingredient = {
  id: Uuid; product: ProductRef;
  quantity?: Quantity;            // null ⇒ "do smaku"
  baseAmount?: number;            // server-computed canonical amount (g | ml | medium pieces); null iff quantity null
  note?: string; optional: boolean;
};
type Step = { id: Uuid; blocks: ContentBlock[] };

type RecipeDetail = {
  id: Uuid; version: Version; isArchived: boolean;
  visibility: Visibility; isFavorite: boolean;
  canEdit: boolean;               // caller is author && visibility !== 'public' && !isArchived
  totalVariantCount: number;      // all variants, including ones hidden from the caller (author only; others get 0)
  foreignVariantCount: number;    // variants by other users (author only; others get 0)
  title: string; description?: string; coverImage?: ImageInfo;
  author: UserRef; prepMinutes?: number; cookMinutes?: number;
  tags: TagRef[];                 // includes system tag 'szybkie' when applicable
  utensils: UtensilRef[];
  ingredients: Ingredient[]; prepSteps: Step[]; instructionSteps: Step[];
  cookCount: number; variantCount: number;   // variantCount = visible to the caller
  createdAt: DateTime; updatedAt: DateTime;
};

type IngredientWrite = {
  id?: Uuid;                      // keep existing id on edit (needed for variant ops!); omit for new rows
  productId: Uuid; quantity?: Quantity; note?: string; optional: boolean;
};
type StepWrite = { id?: Uuid; blocks: ContentBlock[] };   // ≥ 1 block
type RecipeWrite = {
  title: string; description?: string; coverImageId?: Uuid;
  prepMinutes?: number; cookMinutes?: number;             // 0–2880
  tagIds: Uuid[];                                          // system tags ignored
  utensilIds: Uuid[];
  ingredients: IngredientWrite[];                          // 1–100
  prepSteps: StepWrite[]; instructionSteps: StepWrite[];  // 0–50 each; instructions ≥ 1
};
```
**Id stability:** on `PUT`, rows sent with an `id` that exists in the recipe are updated in place. Rows without an id are created, and missing rows are deleted.

## 5. Variants

| Method | Path | Body / query | Response | Errors |
|---|---|---|---|---|
| GET | `/recipes/{id}/variants` | — | `200 VariantSummary[]` (visible to the caller) | 404 |
| GET | `/variants` | `author=me` (required in v1), `page`, `pageSize` | `200 Paged<VariantSummary>` (includes variants whose base is no longer visible, with `baseAvailable: false`) | |
| GET | `/variants/{id}` | — | `200 VariantDetail` | 404 `variant.not_found` (also when the variant or its recipe isn't visible; the author always gets it) |
| GET | `/variants/{id}/resolved` | — | `200 ResolvedRecipe` | 404 |
| POST | `/recipes/{id}/variants` | `VariantWrite` | `201 VariantDetail` | 404 (recipe not visible), 400 `variant.no_changes`, 400 `quantity.unit_incompatible`, 409 `variant.name_taken` |
| PUT | `/variants/{id}` | `VariantWrite & { version: Version }` | `200 VariantDetail` | 403 `variant.not_author`, 409 |
| PUT | `/variants/{id}/favorite` | — | `204` (idempotent) | 404 |
| DELETE | `/variants/{id}/favorite` | — | `204` (idempotent) | |
| DELETE | `/variants/{id}` | — | `204` | 403 |
| GET | `/recipes/{id}/variant-matrix` | — | `200 VariantMatrix` | 404 |
| GET | `/recipes/{id}/my-selection` | — | `200 Selection` or `204` if none | |
| PUT | `/recipes/{id}/my-selection` | `SelectionWrite` | `200 Selection` | 400 `selection.unknown_slot` (unknown keys are dropped with a warning instead of failing if they reference deleted variants) |

**Writing variants (D-12):** the client sends the *full edited recipe*, and the server diffs it against the current base.

```ts
type VariantWrite = {
  name: string;                 // 1–80 chars, unique per recipe per author
  description?: string;
  visibility: Visibility;       // effective audience = recipe audience ∩ variant audience (PROJECT §6.1)
  recipe: Omit<RecipeWrite, 'title' | 'description' | 'coverImageId' | 'tagIds'>;  // edited copy; ids of base rows must be kept
};

type IngredientOp =
  | { op: 'modify'; targetId: Uuid; productId: Uuid; quantity?: Quantity; note?: string; optional: boolean }
  | { op: 'remove'; targetId: Uuid }
  | { op: 'add'; id: Uuid; afterId?: Uuid; productId: Uuid; quantity?: Quantity; note?: string; optional: boolean };
type StepOp =
  | { op: 'replace'; section: StepSection; targetId: Uuid; blocks: ContentBlock[] }
  | { op: 'remove'; section: StepSection; targetId: Uuid }
  | { op: 'add'; section: StepSection; id: Uuid; afterId?: Uuid; blocks: ContentBlock[] };
// afterId null ⇒ insert at the start. `afterId` may reference a base row or an earlier 'add' in the same variant.

type VariantSummary = {
  id: Uuid; recipeId: Uuid; recipeTitle: string; name: string; description?: string;
  author: UserRef; visibility: Visibility; isFavorite: boolean; baseAvailable: boolean;
  cookCount: number; changeCount: number;
  createdAt: DateTime; updatedAt: DateTime;
};
type VariantDetail = VariantSummary & {
  version: Version;
  ingredientOps: IngredientOp[]; stepOps: StepOp[];
  utensilIdsAdded: Uuid[]; utensilIdsRemoved: Uuid[];
  prepMinutes?: number; cookMinutes?: number;   // overrides; null = inherit
};

type ResolvedRecipe = Omit<RecipeDetail, 'version' | 'variantCount' | 'totalVariantCount' | 'foreignVariantCount' | 'canEdit'> & {
  variantId?: Uuid;              // null ⇒ base
  variantName?: string;
};
```
In a resolved recipe, ingredient ids are kept from the base, and added ingredients use their op `id`.

### 5.1 Variant matrix (input to client-side auto-match)

```ts
type VariantMatrix = {
  recipeId: Uuid;
  variants: MatrixVariant[];      // includes the base as { id: 'base', … }; only variants visible to the caller
  slots: Slot[];                  // ordered: base ingredients in base order, then added slots in first-seen order
};
type MatrixVariant = {
  id: Uuid | 'base'; name: string; author: UserRef; cookCount: number; createdAt: DateTime;
  isFavorite: boolean;            // caller's ♥ (for 'base': the recipe's ♥)
  productSwaps: { fromProductId: Uuid; toProductId: Uuid }[];   // for preference scoring
};
type Slot = {
  key: string;                    // base: "i:<ingredientId>"  |  added: "p:<productId>"
  label: string;                  // product name of the base row / added product
  afterSlotKey?: string;          // added slots: the base slot key the row follows in the first variant that adds it
                                  // (nearest base row before it), null at the start; null for base slots.
                                  // Optional in openapi.yaml (older fixtures stay valid); the server always sends it.
  options: SlotOption[];
};
type SlotOption = {
  key: string;                    // "none" | "<productId>:<baseAmount rounded to 3 decimals or 'x' if null>"
                                  // amount in invariant format without trailing zeros: "…:27.3", "…:200"
  ingredient?: Ingredient;        // null for "none"
  variantIds: (Uuid | 'base')[];  // every variant whose resolved recipe has this value in this slot
};
```
Equality rule: the same product and base amounts within 1 % produce the same option, keyed by the first-seen amount. Every variant appears in **exactly one** option per slot. A variant that lacks an added slot appears in its `none` option.

### 5.2 Selection memory

```ts
type Pick = { optionKey: string; source: 'user' | 'auto' };
type ScalingState = { anchorSlotKey: string; anchorBaseAmount: number };   // user's "Mam: …" in base units
type SelectionWrite = { picks: Record<string /*slotKey*/, Pick>; scaling?: ScalingState };
type Selection = SelectionWrite & { recipeId: Uuid; updatedAt: DateTime };
```
On `PUT`, the server recomputes the user's product preferences from the `source: 'user'` picks whose product differs from the base slot product (weight = the number of recipes in which the swap is pinned).

### 5.3 Favourites list

| Method | Path | Query | Response |
|---|---|---|---|
| GET | `/favorites` | `page`, `pageSize` | `200 Paged<FavoriteItem>`, newest favourite first, only items currently visible to the caller (PROJECT §4.6) |

```ts
type FavoriteItem =
  | { kind: 'recipe'; favoritedAt: DateTime; recipe: RecipeSummary }
  | { kind: 'variant'; favoritedAt: DateTime; variant: VariantSummary; recipe: RecipeSummary };
```

## 6. Fridges

| Method | Path | Body | Response | Errors |
|---|---|---|---|---|
| GET | `/fridges` | — | `200 FridgeSummary[]` | |
| POST | `/fridges` | `{ name: string }` | `201 FridgeSummary` | |
| GET | `/fridges/{id}` | — | `200 FridgeDetail` | 404 `fridge.not_found` (also for non-members) |
| PUT | `/fridges/{id}` | `{ name: string }` | `200 FridgeSummary` | 403 `fridge.owner_required` |
| DELETE | `/fridges/{id}` | — | `204` | 403 |
| POST | `/fridges/{id}/invites` | — | `201 Invite` | 403 |
| GET | `/fridges/{id}/invites` | — | `200 Invite[]` (active) | 403 |
| DELETE | `/fridges/{id}/invites/{inviteId}` | — | `204` | 403 |
| GET | `/invites/{token}` | — | `200 InvitePreview` | 404 `invite.not_found` (also expired/revoked) |
| POST | `/invites/{token}/accept` | — | `200 FridgeSummary` (idempotent for existing members) | 404 |
| DELETE | `/fridges/{id}/members/{userId}` | — | `204` (owner removes anyone except self; member removes self) | 403, 400 `fridge.owner_cannot_leave` |
| GET | `/fridges/{id}/items` | — | `200 FridgeItem[]` | 404 |
| POST | `/fridges/{id}/items` | `FridgeItemWrite` | `201 FridgeItem` | 404 |
| PUT | `/fridges/{id}/items/{itemId}` | `FridgeItemWrite & { version }` | `200 FridgeItem` | 409 |
| DELETE | `/fridges/{id}/items/{itemId}` | — | `204` | |

```ts
type FridgeSummary = { id: Uuid; name: string; role: FridgeRole; owner: UserRef; memberCount: number; itemCount: number };
type FridgeDetail = FridgeSummary & { members: (UserRef & { role: FridgeRole })[] };
type Invite = { id: Uuid; token: string; url: string /* "/zaproszenie/<token>" */; expiresAt: DateTime; createdAt: DateTime };
type InvitePreview = { fridgeName: string; owner: UserRef; expiresAt: DateTime; alreadyMember: boolean };
type FridgeItem = {
  id: Uuid; version: Version; product: ProductRef;
  packageAmount: number;        // base units, > 0
  remainingPercent: number;     // 0–100, up to 2 decimals
  remainingAmount: number;      // packageAmount * remainingPercent / 100 (server-computed)
  note?: string; addedBy: UserRef; createdAt: DateTime; updatedAt: DateTime;
};
type FridgeItemWrite = { productId: Uuid; packageAmount: number; remainingPercent: number; note?: string };
```
A `PUT` with `remainingPercent = 0` deletes the item and returns `204`.

## 7. Deductions and shopping list

| Method | Path | Body | Response | Errors |
|---|---|---|---|---|
| POST | `/fridges/{id}/deductions` | `DeductionRequest` | `201 DeductionResult` | 404, 400 `deduction.empty` |
| POST | `/fridges/{id}/deductions/{deductionId}/undo` | — | `200 DeductionResult` (with `undone: true`) | 409 `deduction.items_changed`, 409 `deduction.already_undone` |
| GET | `/fridges/{id}/shopping-list` | — | `200 ShoppingListItem[]` (unchecked first, then by category, then name) | 404 |
| POST | `/fridges/{id}/shopping-list/items` | `{ items: ShoppingListItemWrite[] }` (1–100) | `200 ShoppingListItem[]` (created or merged) | 400 |
| PATCH | `/fridges/{id}/shopping-list/items/{itemId}` | `ShoppingListItemPatch` | `200 ShoppingListItem` | 409 |
| DELETE | `/fridges/{id}/shopping-list/items/{itemId}` | — | `204` | |
| POST | `/fridges/{id}/shopping-list/complete` | `{ moveToFridge: boolean }` | `200 { removed: number; createdFridgeItems: FridgeItem[] }` | |

```ts
type DeductionRequest = {
  recipeId?: Uuid; variantId?: Uuid;              // for cook counts; variantId requires recipeId
  lines: { productId: Uuid; amount: number }[];  // base units, > 0; same product may not repeat
};
type DeductionResult = {
  id: Uuid; undone: boolean; createdAt: DateTime;
  lines: {
    productId: Uuid; requested: number; deducted: number; shortfall: number;
    items: { itemId: Uuid; previousPercent: number; newPercent: number; removed: boolean }[];
  }[];
};

type ShoppingListItem = {
  id: Uuid; version: Version;
  product?: ProductRef; text?: string;            // exactly one of product / text
  amount?: number;                                // base units, only with product
  checked: boolean; addedBy: UserRef; createdAt: DateTime;
};
type ShoppingListItemWrite = { productId?: Uuid; text?: string; amount?: number };
type ShoppingListItemPatch = { version: Version; checked?: boolean; amount?: number; text?: string };
```
Merge rule: posting a product that already has an **unchecked** item adds the amounts together (null + x = x).

## 8. Recipe export format (`RecipeExport`)

```ts
type RecipeExport = {
  format: 'recipes-list/recipe'; formatVersion: 1; exportedAt: DateTime;
  products: ExportProduct[];                       // every product referenced anywhere below
  images: Record<string /*localImageKey*/, { contentType: 'image/webp'; dataBase64: string }>;
  recipe: {
    key: string; title: string; description?: string; coverImageKey?: string; authorNickname: string;
    prepMinutes?: number; cookMinutes?: number; tags: string[]; utensils: string[];
    ingredients: { key: string; productKey: string; quantity?: Quantity; note?: string; optional: boolean }[];
    prepSteps: { key: string; blocks: ExportBlock[] }[];
    instructionSteps: { key: string; blocks: ExportBlock[] }[];
  };
  variants: {
    name: string; description?: string; authorNickname: string;
    prepMinutes?: number; cookMinutes?: number; utensilsAdded: string[]; utensilsRemoved: string[];
    ingredientOps: unknown[]; stepOps: unknown[];  // same shapes as §5 but ids → keys, productId → productKey, imageId → imageKey
  }[];
};
type ExportProduct = Omit<ProductWrite, never> & { key: string };
type ExportBlock = { type: 'text'; text: string } | { type: 'image'; imageKey: string; caption?: string };
```
Keys are opaque strings that are unique within the file (the exporter uses `i1, i2…`, `s1…`, `p1…`, `img1…`). Import matches products by normalized name and creates any that are missing, using the exported properties.

## 9. Error codes

`auth.invalid_token` · `auth.required` · `user.onboarding_required` · `nickname.taken` · `nickname.invalid` · `admin.required` ·
`product.not_found` · `product.duplicate` · `product.invalid_combination` · `product.merge_incompatible` · `tag.reserved` ·
`image.invalid_type` · `image.too_large` · `image.not_found` · `image.not_owned` ·
`recipe.not_found` · `recipe.not_author` · `recipe.public_locked` · `recipe.has_variants` · `recipe.visibility_locked` · `recipe.archived` · `quantity.unit_incompatible` · `quantity.invalid` ·
`variant.not_found` · `variant.not_author` · `variant.no_changes` · `variant.name_taken` · `selection.unknown_slot` ·
`fridge.not_found` · `fridge.owner_required` · `fridge.owner_cannot_leave` · `invite.not_found` ·
`fridge_item.not_found` · `deduction.empty` · `deduction.not_found` · `deduction.items_changed` · `deduction.already_undone` ·
`shopping_item.not_found` · `shopping_item.invalid` ·
`import.invalid_format` · `import.unsupported_version` · `import.too_large` ·
`concurrency.conflict` · `validation.failed` · `request.missing_csrf_header` · `request.not_found` · `request.forbidden` · `request.invalid` · `rate_limited` · `server.error`

Generic codes for responses that no feature produced: `auth.required` (401, not signed in), `request.not_found` (404, unknown route), `request.forbidden` (403), `request.invalid` (other 4xx such as malformed JSON or a wrong content type), `server.error` (5xx, including the 501 stubs before a feature is implemented).

Every code needs an entry in `frontend/src/lib/i18n/pl/errors.ts`. A unit test checks that every code in this list has one.
