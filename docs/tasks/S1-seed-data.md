# S1: Polish seed data

**Wave 1 · depends on: T00 (schemas in `shared/seed/*.schema.json`)**

## Goal
Realistic starting data so that the app is useful from day one.

## Read first
PROJECT §4.3, §5, §7.2.

## Owns
`shared/seed/products.json`, `shared/seed/tags.json`, `shared/seed/utensils.json`.

## Deliverables
- **≥ 150 products** typical of Polish home cooking, covering every category. Each has a correct `measureKind` and `displayClass`, plus:
  - `gramsPerMl` for products that are commonly measured in spoons or glasses but are mass products (flour ≈ 0.53, sugar ≈ 0.85, butter ≈ 0.91, honey ≈ 1.42, rice ≈ 0.85, salt ≈ 1.2…), and for volume products where weight matters (oil ≈ 0.92, milk ≈ 1.03, cream ≈ 1.0),
  - `gramsPerPiece` for pieces (onion ≈ 110 g, carrot ≈ 70, potato ≈ 150, garlic clove ≈ 5, egg ≈ 55, tomato ≈ 120, lemon ≈ 100…),
  - `hasSizes` for vegetables and fruits that come in sizes; `allowHalf` for lemon, avocado, pepper, onion, cabbage-type products,
  - `defaultPackageAmount` for packaged goods (śmietanka 30% 330 ml, mleko 1000 ml, masło 200 g, jogurt naturalny 400 g, passata 500 ml, ser żółty 150 g, jajka 10 szt.…),
  - `pieceForms` where `szt.` reads badly (`ząbek/ząbki/ząbków/ząbka`, `puszka/puszki/puszek/puszki`, `jajko/jajka/jajek/jajka`, `pęczek/pęczki/pęczków/pęczka`, `liść/liście/liści/liścia`).
- **≥ 25 tags** (meal types, diets, cuisines, occasions, techniques), plus the system tag `szybkie` (`isSystem: true`).
- **≥ 30 utensils** (piekarnik, patelnia, garnek, blender, mikser, tarka, sitko, wałek, forma do pieczenia, termometr kuchenny…).
- Names: Polish, lower-case except proper nouns, natural forms (`pierś z kurczaka`, `śmietana 18%`), and no duplicates after normalization.

## Acceptance
- Every file validates against its schema (add a tiny Node or C# check script in `shared/seed/validate.*` and run it).
- No two entries share a normalized name. Every `displayClass` matches its `measureKind`.
- Densities and piece weights cite a rough source in a sidecar `shared/seed/SOURCES.md` (general cooking conversion tables are fine).

## Report

### Summary
- `shared/seed/products.json`: 249 products over all 15 categories, with densities, piece weights, sizes/halves, default packages and `pieceForms` (ząbek, puszka, jajko, pęczek, liść, główka, kostka and others).
- `shared/seed/tags.json`: 35 tags including the system tag `szybkie`. `shared/seed/utensils.json`: 50 utensils.
- `shared/seed/validate.mjs`: dependency-free validator (run `node shared/seed/validate.mjs`). It checks each file against its schema (a JSON Schema subset interpreter), duplicates after normalization (mirrors `NameNormalizer`), displayClass/measureKind consistency, count products having `gramsPerPiece`, category coverage and minimum counts.
- `shared/seed/SOURCES.md`: provenance of densities, weights and package sizes.

### Deviations
- The validator uses its own small schema interpreter instead of `ajv`, so it runs with plain `node` and no install. It supports only the keywords used by the current schemas; a new keyword in a schema would need support added.
- Extra rules beyond the schema: every `count` product must have `gramsPerPiece`; names must start lower-case. A few mass products (musztarda, miód, dżem...) carry `gramsPerMl` for spoon measures.

### Model changes
None.

### Contract changes
None.

### Dependencies added
None.

### Requests for other tasks
- B2: the seeder should upsert by `NameNormalizer.Normalize(name)`; all names are already unique under it. `pieceForms` keys are `one/few/many/fraction`.

### Follow-ups
- Piece weights and densities are rough; admins can edit products later (PROJECT §7.3).
