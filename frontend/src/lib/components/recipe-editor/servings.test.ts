import { describe, expect, it } from 'vitest';
import { t } from '$lib/i18n';
import { baseRecipe } from '../../../../tests/integration/mocks/variants';
import {
	buildBody,
	draftFromRecipe,
	emptyDraft,
	toVariantRecipeWrite,
	validateDraft
} from './draft';

// "Liczba porcji" in the recipe and variant editors (PROJECT §4.1, §5.7).
describe('editor servings', () => {
	it('a new draft has no servings and sends none', () => {
		const draft = emptyDraft();
		expect(draft.servings).toBeNull();
		expect(buildBody(draft).body).not.toHaveProperty('servings');
	});

	it('round-trips the servings of a recipe into both write shapes', () => {
		const draft = draftFromRecipe(baseRecipe({ servings: 4 }));
		expect(draft.servings).toBe(4);
		expect(buildBody(draft).body.servings).toBe(4);
		expect(toVariantRecipeWrite(draft, []).recipe.servings).toBe(4);
	});

	it('a cleared field is left out (the variant then inherits)', () => {
		const draft = { ...draftFromRecipe(baseRecipe({ servings: 4 })), servings: null };
		expect(toVariantRecipeWrite(draft, []).recipe).not.toHaveProperty('servings');
	});

	it.each([
		[0, true],
		[1, false],
		[100, false],
		[101, true],
		[2.5, true]
	])('validates %d servings (error: %s)', (servings, hasError) => {
		const draft = { ...draftFromRecipe(baseRecipe()), servings };
		const errors = validateDraft(draft);
		expect(errors.servings).toBe(hasError ? t.recipes.fieldErrors.servingsRange : undefined);
	});
});
