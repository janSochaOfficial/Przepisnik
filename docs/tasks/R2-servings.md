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

## Report

### Summary
Servings with scaling, full stack (branch `task/R2-servings`).
- Backend: `Recipe.Servings` and `Variant.ServingsOverride` (validated 1–100, 400 `validation.failed`); `RecipeDetail`, `ResolvedRecipe` (`servingsOverride ?? recipe.servings`), `VariantDetail.servingsOverride`, `changeCount` counts it. The diff emits an override only when the edited copy's servings differ from the base's. Export carries `recipe.servings` and `variants[].servingsOverride` (`formatVersion` stays 1; the validator checks the override range); import creates them; `ImportAlignment` takes the file's servings or keeps the target's, and `HasSameContent` compares them. `ScalingState` gained `Kind` (`ingredient` default, `servings`) + `Servings`; old jsonb rows without `kind` read as the ingredient form. `PUT my-selection` validates both forms (400 `validation.failed`), and a servings choice is never dropped as stale (only an ingredient anchor is).
- Frontend: "Liczba porcji" `NumberField` (1–100) next to the times in the recipe and variant editors (`draft.ts`: draft, body, validation). New `recipe-view/ServingsControl.svelte` ("Porcje: [−] N [+]", typing with Enter/blur, range message) in the ingredients section, only when the shown recipe has servings. `SelectionState`: `servings` (from the steps source, so a variant's override), `factor` for both forms, `shownServings` (`servings × factor` rounded to ½ with the `~` rule when scaled from an ingredient, with a "Przeliczone składniki wystarczą na …" line), `setServings`. The banner and "Przywróć" cover both forms ("Przeliczono ×1,5 według: 6 porcji"). Switching variants keeps a servings choice and recomputes the factor against the new variant's servings; deduction and "Dodaj brakujące" use `cookIngredients` as before. `domain/scaling.ts`: `servingsFactor`, `clampServings`, `scaledServings`, `formatServings` with the forms `porcja / porcje / porcji / porcji` (in `i18n/pl/recipes.ts`).
- Tests: backend unit `Variants/ServingsTests.cs` (validation bounds, diff same/different/cleared, resolve, alignment, export validator), integration `Recipes/ServingsTests.cs` (CRUD, variant override, export → import round trip, file without servings, import as variants, `my-selection` both forms + 6 invalid shapes + unknown kind, legacy jsonb row, servings choice survives a variant delete); Vitest `domain/servings.test.ts`, `state/servings.svelte.test.ts`, `recipe-editor/servings.test.ts`; Playwright `tests/integration/servings.spec.ts` (+ `mocks/servings.ts`) incl. axe light/dark. `scripts/verify.sh --agent` green after rebasing onto main.

### Deviations
- **`~` on "×0.6 on 4 → ~2 ½ porcji"**: 4 × 0.6 = 2.4, and 2 ½ is 4.2 % away, so the §5.4 rule that §5.7 references ("~ when more than 5 % off") gives `2 ½ porcji` without `~`. PROJECT.md wins over the task file: the test asserts `2 ½ porcji` for ×0.6 and `~2 ½ porcji` for ×0.575 (2.3).
- **Clearing the servings in a variant inherits** (the acceptance's "cleared → inherit"), whereas clearing a base time is rejected with 400. Same value as the base is no op.
- `servings` / `servingsOverride` are optional (not `required`) in openapi responses although the server always sends them, so existing mocks and fixtures stay valid (task: `servings?: integer | null`).
- The ingredient form of `ScalingState` is still sent without `kind` by the client (allowed by the contract); the server always answers with `kind`.
- An unknown `kind` (e.g. `"people"`) is a JSON binding error → 400 `request.invalid`, not `validation.failed`.
- The NumberField turns out-of-range text into `null` with its own message ("Największa wartość to 100."), as for the times, so the editor does not block saving on it; the client validator (`servingsRange`) covers non-null out-of-range values.
- No shared engine vectors: `shared/test-vectors/schemas/variants.schema.json` (not mine) has no servings, so the C# vector reader passes `servings: null`; servings are covered by unit tests instead.
- Touched outside the strict list (additive): `routes/przepisy/[id]/+page.svelte` (ServingsControl and the banner text for the servings form), `RecipeQueries.cs`, `RecipeDtos.cs`, `VariantDtos.cs`, `VariantService.cs`/`VariantQueries.cs`/`SelectionMemory.cs`/`RecipeSelection.cs`, and the unit-test helpers `RecipeBuilder.cs`, `VariantEnginePropertyTests.cs`, `VariantVectorJson.cs`, `RecipeInputValidatorTests.cs` (constructor arity only).

### Model changes
- `recipes.servings integer NULL` with check constraint `ck_recipes_servings_range` (`servings BETWEEN 1 AND 100`).
- `variants.servings_override integer NULL` with check constraint `ck_variants_servings_override_range` (`servings_override BETWEEN 1 AND 100`).
- `recipe_selections.scaling` (jsonb) may now hold `{"kind":"servings","servings":N}`; existing rows stay valid (no data migration).
No migration added (plan §4.2); the coordinator generates it.

### Contract changes
API.md §4 (`RecipeDetail.servings`, `RecipeWrite.servings`), §5 (`VariantWrite.recipe.servings`, `VariantDetail.servingsOverride`, `ResolvedRecipe.servings`), §5.2 (`ScalingState` union + rules), §8 (`recipe.servings`, `variants[].servingsOverride`, servings paragraph). openapi.yaml: the same fields; `ScalingState` is now `oneOf: [IngredientScalingState, ServingsScalingState]`. `schema.d.ts` regenerated.

### Dependencies added
None.

### Requests for other tasks
- I2 (`i18n/pl/import-format.ts`, the "Format pliku" help page): add `servings` (recipe: "Liczba porcji, liczba całkowita 1–100, opcjonalna") and `servingsOverride` (variant: "1–100 albo null (bez zmiany)") rows and, optionally, to the example. Not edited here because the file belongs to I2.

### Follow-ups
- `RecipeSummary` (cards) does not carry servings; add it if cards should show them.
- The servings control lives in the ingredients section only; it is not repeated in the deduction dialog (which uses the scaled amounts as before).
