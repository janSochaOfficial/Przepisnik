# R2: Servings with scaling (full stack)

**Wave v1.2 · depends on: R1 merged (recipe view and `SelectionState`) · slot 3 · model `opus`, effort `medium` (data model, contract, variant engine, export format and scaling state all change together: high compound risk)**

## Goal
Recipes have an optional number of servings that variants can override; choosing a servings count in the recipe view scales the recipe (PROJECT §4.1, §5.7, §6.1, D-37).

## Read first
PROJECT §4.1, §4.7, §5 (all, especially the new §5.7), §6.1, §6.6 · API §4, §5, §8 · TECHNICAL §3 · the B4 variant code (`Core/Variants/**`), the B6/I1 transfer code (`Core/Transfer/**`, `ImportAlignment`) · `src/lib/state/selection.svelte.ts`, `src/lib/domain/scaling.ts`.

## Skills
`dotnet-webapi`, `optimizing-ef-core-queries`, `svelte-core-bestpractices`, `svelte-code-writer` (autofixer on every changed `.svelte` / `.svelte.ts`).

## Contract (API.md + openapi.yaml together)
- `RecipeWrite`, `RecipeDetail`, `ResolvedRecipe`, the export recipe: `servings?: integer | null` (1–100; 400 `validation.failed` outside).
- `VariantWrite` (the edited full copy) carries `servings`; the diff produces a **servings override** like the time overrides; `VariantDetail` and the export variant carry `servingsOverride?: integer | null`. `null` = inherit (as for times).
- `ScalingState` gains a second form: `{ kind: 'servings'; servings: integer }` next to the existing anchor form (`{ kind?: 'ingredient'; anchorSlotKey; anchorBaseAmount }`; a missing `kind` means ingredient, so stored selections stay valid). Prefer a discriminated union in openapi; document it in API §5.
- Export: `formatVersion` stays 1 (the fields are optional; files without them import as before). I1's alignment: a servings override when the file's servings differ, and a missing value keeps the target's (like times).

## Model changes (report them; no migration, plan §4.2)
`recipes.servings int null`, `variants.servings_override int null`, with a check constraint 1–100 if the configuration style allows it.

## Owns
`Core/Recipes/**`, `Core/Variants/**` (servings in resolve/diff only), `Core/Transfer/**` (servings fields only), the matching `Api/Features/**` DTO mapping, the entity configurations of `Recipe` and `Variant`, their tests, `shared/test-vectors/variants/R2.json` if the engine vectors need it; frontend: the servings field in `components/recipe-editor/**` and the variant editor, a new `components/recipe-view/ServingsControl.svelte`, the servings parts of `selection.svelte.ts` and `domain/scaling.ts` (+ tests), the plural forms in `domain` (`porcja / porcje / porcji / porcji`), the servings keys in `i18n/pl/recipes.ts` / `variants.ts`, mocks and specs.

## Deliverables
- Backend: validation, persistence, resolve (`servingsOverride ?? recipe.servings`), diff (an override only when it differs), selection write accepting both scaling forms, export/import both ways, alignment.
- Recipe and variant editor: an optional "Liczba porcji" `NumberField` (1–100) next to the times.
- Recipe view: `ServingsControl` in the ingredients section when the resolved recipe has servings: "Porcje: [−] N [+]", typing allowed. Choosing M sets the single scaling choice to servings (factor `M / servings`), replacing an ingredient anchor; an ingredient "Mam:" replaces a servings choice. Scaled from an ingredient, the line shows `servings × factor` rounded to ½ with the `~` rule. The banner and "Przywróć" cover both forms. The deduction dialog and "Dodaj brakujące" use the scaled amounts as today.
- Switching variants keeps a servings choice (it is about people, not a slot); the factor is recomputed against the new variant's servings.

## Acceptance
- Unit tests: validation bounds; resolve and diff of the override (same → no op, different → op, cleared → inherit); `ImportAlignment` servings; frontend factor/rounding (`4 → 6 porcji` = ×1.5; anchor ×0.75 on 4 servings → `3 porcje`; ×0.6 on 4 → `~2 ½ porcji`), plural forms.
- Integration: create/read/update a recipe with servings; a variant overriding them resolves correctly; export → import round trip keeps both; `PUT my-selection` with each scaling form and an invalid one (400).
- Playwright: set 4 servings in the editor; in the view change to 6 → amounts ×1.5, banner, "Przywróć"; a recipe without servings shows no control; axe light + dark.
- `scripts/verify.sh --agent` is green.
