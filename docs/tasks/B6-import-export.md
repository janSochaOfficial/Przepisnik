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

## Report

### Summary
- `GET /recipes/{id}/export` and `POST /recipes/import` are implemented (`Features/Transfer/`: `TransferFeature`, `RecipeExporter`, `RecipeImporter`; `Core/Transfer/`: format models, `ExportValidator`, `ImportFooter`).
- Export: recipe plus the variants visible to the caller; keys `i/s/p/img` numbered in a fixed traversal order; products with full properties; photos as base64 WebP; no visibility, archived flag or system tag.
- Import: 25 MB cap (413 `import.too_large`), rate limit `import`, format/version check first, reference validation, then one `TransactionScope`: photos (owner = importer), products/tags/utensils matched by normalized name through `ProductCatalog`/`NameCatalogs` (`szybkie` ignored), `RecipeService.CreateAsync` (private), then private variants with remapped ops. Failures roll back and delete stored photos; all content errors become 400 `import.invalid_format`.
- Tests: 19 integration tests (round trip with 2 variants, step and variant-op photos, removed and added ingredient; unknown product created; other-case name reused; broken reference with nothing persisted; rollback after catalog rows and photos exist; version 2; malformed files; 413; 429; sample file) and 16 unit tests. `scripts/verify.sh --agent` is green.
- `shared/samples/export-v1.json` now exists and is imported by a test. F4/T90 can switch their local import fixture to it.
- `AuthFoundationTests`: the 501 stub test used the export route and no stubs remain, so I removed that test. `StubEndpoints.cs` is now unused (left in place; the coordinator may delete it).

### Deviations
- Export strips the import footer from descriptions and reports the nickname in it as `authorNickname` (recipe and variants), which makes `export(import(export(R)))` equal `export(R)` and preserves the original author across chains. The footer is also added to variant descriptions (PROJECT says each keeps the note).
- If description plus footer exceeds the limit (2000), the description is cut with "…" instead of rejecting the file.
- Variant names that clash after import (same name from different authors) get " (2)", " (3)" suffixes, because names are unique per recipe and author.
- Photos are re-encoded on import (storage pipeline), so photo bytes differ; the round-trip test compares photo dimensions.
- Spec said op fields keep shapes with ids to keys; I fixed the exact names: `id`→`key`, `targetId`→`targetKey`, `afterId`→`afterKey`, `productId`→`productKey`, `imageId`→`imageKey`; `baseAmount` is not exported.
- A concurrent create of the same new product/tag inside the import transaction would surface as an error (500/409) instead of retrying; rare, not handled.
- Missing photo file in storage during export gives a 500.

### Model changes
None.

### Contract changes
Description text only, in `docs/API.md` §8 and `docs/api/openapi.yaml` (`ExportVariant.ingredientOps/stepOps`): exact op key names, footer behaviour, system tags not exported. `schema.d.ts` regenerated.

### Dependencies added
None.

### Requests for other tasks
- F4/T90: use `shared/samples/export-v1.json` as the import fixture.

### Follow-ups
- Delete `StubEndpoints.cs` if no stubs are planned.
