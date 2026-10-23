# B6: Recipe JSON export and import (backend)

**Wave 2 · depends on: B3, B4 merged**

## Goal
`GET /recipes/{id}/export` and `POST /recipes/import`, as specified in API §8 and PROJECT §4.7.

## Read first
PROJECT §4.4, §4.7 · API §4, §5, §8.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`.

## Owns
`*/Features/Transfer/**`, `backend/src/RecipesList.Core/Transfer/**`, tests, `backend/http/transfer.http`, `shared/samples/export-v1.json` (a sample file).

## Deliverables
- Export: any caller who can see the recipe. It includes the recipe plus every variant **visible to the caller** (including other authors'). Visibility settings are not exported. Products are serialized with full `ProductWrite` properties; every referenced image (cover, step blocks, variant step ops) is base64 WebP. Opaque keys replace every id; op references (`targetId`, `afterId`, `productId`, `imageId`) are remapped to keys. The archived flag is not exported.
- Import: validate the format and version (a JSON schema in `shared/` is fine); 25 MB limit (413 `import.too_large`); rate limit `import`. In **one transaction**: match or create products by normalized name, match or create tags and utensils (ignore `szybkie`), store the images (owner = importer), create the recipe (author = importer, visibility `private`, with the description footer `Zaimportowano — oryginalnie: @<authorNickname>`; put the footer string in a constant), and create the variants (author = importer, visibility `private`, op keys remapped to the new ids).
- Round-trip guarantee: `export(import(export(R)))` equals `export(R)` modulo keys, timestamps and author fields.

## Acceptance
- The round-trip test above, on a recipe with 2 variants, images in steps and in a variant step op, a removed ingredient, and an added ingredient.
- Import with an unknown product → product created; with an existing product name in a different case → reused.
- A broken reference (an op pointing at a missing key) → 400 `import.invalid_format`, with nothing persisted.
- `formatVersion: 2` → 400 `import.unsupported_version`.
