import { describe, expect, it } from 'vitest';
import type { ProductRef } from '$lib/domain/types';
import {
	compatibleUnits,
	defaultUnit,
	draftFromRecipe,
	draftSignature,
	emptyDraft,
	emptyIngredient,
	mapServerErrors,
	toRecipeWrite,
	toVariantRecipeWrite,
	validateDraft,
	type RecipeDraft
} from './draft';

const base = {
	hasSizes: false,
	allowHalf: false,
	category: 'other',
	displayClass: 'weight',
	gramsPerMl: null,
	gramsPerPiece: null,
	defaultPackageAmount: null,
	pieceForms: null
} as const;
const flour: ProductRef = { ...base, id: 'p-flour', name: 'mąka', measureKind: 'mass' };
const flourDense: ProductRef = { ...flour, gramsPerMl: 0.5 };
const milk: ProductRef = {
	...base,
	id: 'p-milk',
	name: 'mleko',
	measureKind: 'volume',
	displayClass: 'liquid',
	gramsPerMl: 1.03
};
const onion: ProductRef = {
	...base,
	id: 'p-onion',
	name: 'cebula',
	measureKind: 'count',
	displayClass: 'piece',
	hasSizes: true
};

describe('compatibleUnits', () => {
	it('offers only mass units for a mass product without density', () => {
		expect(compatibleUnits(flour)).toEqual(['g', 'kg']);
	});

	it('adds volume units when the product has a density', () => {
		expect(compatibleUnits(flourDense)).toEqual([
			'g',
			'kg',
			'ml',
			'l',
			'glass',
			'tbsp',
			'tsp',
			'drop',
			'pinch'
		]);
	});

	it('offers volume and mass units for a liquid with density, but no pieces', () => {
		expect(compatibleUnits(milk)).not.toContain('piece');
		expect(compatibleUnits(milk)).toContain('g');
	});

	it('offers only pieces for a count product without weight', () => {
		expect(compatibleUnits(onion)).toEqual(['piece']);
	});

	it('picks the natural default unit', () => {
		expect(defaultUnit(flour)).toBe('g');
		expect(defaultUnit(milk)).toBe('ml');
		expect(defaultUnit(onion)).toBe('piece');
	});
});

function filled(): RecipeDraft {
	const d = emptyDraft();
	d.title = ' Placki ';
	d.ingredients = [
		{ ...emptyIngredient(), id: 'ing-1', product: flour, value: 200, unit: 'g' },
		{
			...emptyIngredient(),
			product: onion,
			value: 2,
			unit: 'piece',
			size: 'large',
			note: ' w kostkę '
		},
		{ ...emptyIngredient(), product: milk, toTaste: true, optional: true }
	];
	const first = d.instructionSteps[0];
	if (first) first.blocks = [{ key: 'b', type: 'text', text: 'Smaż.' }];
	return d;
}

describe('toRecipeWrite', () => {
	it('keeps ingredient ids and builds the contract shape', () => {
		const { write } = toRecipeWrite(filled(), { tagIds: ['t1'], utensilIds: [] });
		expect(write.ingredients[0]?.id).toBe('ing-1');
		expect(write.ingredients[1]).toEqual({
			productId: 'p-onion',
			quantity: { value: 2, unit: 'piece', size: 'large' },
			note: 'w kostkę',
			optional: false
		});
		expect(write.ingredients[2]).toEqual({ productId: 'p-milk', optional: true });
		expect(write.title).toBe('Placki');
		expect(write.tagIds).toEqual(['t1']);
	});

	it('skips removed rows and remembers their positions', () => {
		const d = filled();
		const second = d.ingredients[1];
		if (second) second.removed = true;
		const { recipe, maps } = toVariantRecipeWrite(d, []);
		expect(recipe.ingredients).toHaveLength(2);
		expect(maps.ingredients).toEqual([0, 2]);
	});
});

describe('draftFromRecipe', () => {
	it('round-trips ids and hides the system tag', () => {
		const draft = draftFromRecipe(
			{
				title: 'T',
				description: null,
				coverImage: null,
				prepMinutes: 5,
				cookMinutes: null,
				tags: [
					{ id: 'a', name: 'obiad', isSystem: false },
					{ id: 'b', name: 'szybkie', isSystem: true }
				],
				utensils: [],
				ingredients: [
					{
						id: 'i1',
						product: flour,
						quantity: { value: 1, unit: 'kg' },
						baseAmount: 1000,
						note: null,
						optional: false
					}
				],
				prepSteps: [],
				instructionSteps: [{ id: 's1', blocks: [{ type: 'text', text: 'x' }] }]
			},
			{ base: true }
		);
		expect(draft.tags).toEqual(['obiad']);
		expect(draft.ingredients[0]).toMatchObject({ id: 'i1', value: 1, unit: 'kg', base: true });
		const { write } = toRecipeWrite(draft, { tagIds: [], utensilIds: [] });
		expect(write.instructionSteps[0]?.id).toBe('s1');
	});
});

describe('validateDraft', () => {
	it('flags an empty new recipe', () => {
		const errors = validateDraft(emptyDraft());
		expect(Object.keys(errors).sort()).toEqual(
			['ingredients[0].product', 'ingredients[0].quantity', 'instructionSteps[0]', 'title'].sort()
		);
	});

	it('flags an incompatible unit on the right row', () => {
		const d = filled();
		const row = d.ingredients[1];
		if (row) row.unit = 'g';
		expect(validateDraft(d)['ingredients[1].quantity']).toBeDefined();
	});

	it('accepts a valid draft', () => {
		expect(validateDraft(filled())).toEqual({});
	});
});

describe('mapServerErrors', () => {
	it('maps payload indices to editor rows', () => {
		const out = mapServerErrors(
			{ 'ingredients[1].quantity': ['quantity.unit_incompatible'], title: ['required'] },
			{ ingredients: [0, 2], prepSteps: [], instructionSteps: [] }
		);
		expect(Object.keys(out).sort()).toEqual(['ingredients[2].quantity', 'title']);
	});

	it('collapses block paths onto the step', () => {
		const out = mapServerErrors(
			{ 'instructionSteps[0].blocks[1].text': ['too_long'] },
			{ ingredients: [], prepSteps: [], instructionSteps: [3] }
		);
		expect(Object.keys(out)).toEqual(['instructionSteps[3]']);
	});
});

describe('draftSignature', () => {
	it('ignores row keys but sees edits', () => {
		const a = filled();
		const b = structuredClone(a);
		for (const i of b.ingredients) i.key = 'other';
		expect(draftSignature(a)).toBe(draftSignature(b));
		b.title = 'x';
		expect(draftSignature(a)).not.toBe(draftSignature(b));
	});
});
