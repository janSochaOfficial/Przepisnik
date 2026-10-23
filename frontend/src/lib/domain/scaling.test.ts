import { describe, expect, it } from 'vitest';
import { scaleFactor, scaleIngredients } from './scaling';
import type { Ingredient, ProductRef } from './types';

const product = {
	id: 'p',
	name: 'Mąka',
	measureKind: 'mass',
	displayClass: 'weight'
} as ProductRef;
const ingredient = (baseAmount: number | null): Ingredient => ({
	id: 'i',
	product,
	quantity: baseAmount === null ? null : { value: baseAmount, unit: 'g' },
	baseAmount,
	note: null,
	optional: false
});

describe('scaleFactor', () => {
	it('divides the entered amount by the recipe amount', () => {
		expect(scaleFactor(200, 300)).toBe(1.5);
		expect(scaleFactor(200, 100)).toBe(0.5);
	});

	it.each([
		[0, 100],
		[-5, 100],
		[200, 0],
		[200, -1],
		[200, Number.NaN],
		[200, Number.POSITIVE_INFINITY]
	])('falls back to 1 for the unusable anchor %d / %d', (recipe, user) => {
		expect(scaleFactor(recipe, user)).toBe(1);
	});
});

describe('scaleIngredients', () => {
	it('multiplies every ingredient with a quantity', () => {
		const scaled = scaleIngredients([ingredient(100), ingredient(250)], 2);
		expect(scaled.map((i) => i.scaledBaseAmount)).toEqual([200, 500]);
	});

	it('leaves "do smaku" ingredients without an amount', () => {
		expect(scaleIngredients([ingredient(null)], 3)[0]?.scaledBaseAmount).toBeNull();
	});

	it('keeps the other fields and does not mutate the input', () => {
		const original = ingredient(100);
		const [scaled] = scaleIngredients([original], 2);
		expect(scaled?.product).toBe(product);
		expect(original).not.toHaveProperty('scaledBaseAmount');
	});
});
