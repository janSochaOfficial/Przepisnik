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
