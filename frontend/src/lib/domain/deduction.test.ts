import { describe, expect, it } from 'vitest';
import {
	allAvailable,
	defaultSelection,
	neededFromIngredients,
	planDeduction,
	toDeductionRequest,
	toShoppingLines
} from './deduction';
import type { Ingredient, ProductRef } from './types';

const product = (id: string, over: Partial<ProductRef> = {}): ProductRef =>
	({
		id,
		name: id,
		category: 'other',
		measureKind: 'volume',
		displayClass: 'liquid',
		hasSizes: false,
		allowHalf: false,
		...over
	}) as ProductRef;

const cream = product('cream');
const flour = product('flour', { measureKind: 'mass', displayClass: 'weight' });
const egg = product('egg', { measureKind: 'count', displayClass: 'piece' });
const salt = product('salt', { displayClass: 'spice' });

const have = (p: ProductRef, remainingAmount: number) => ({ product: p, remainingAmount });

describe('planDeduction', () => {
	it('sums several packages of the same product', () => {
		const rows = planDeduction(
			[{ product: cream, amount: 300 }],
			[have(cream, 100), have(cream, 250), have(flour, 999)]
		);
		expect(rows).toEqual([
			{ product: cream, needed: 300, available: 350, status: 'ok', defaultOn: true }
		]);
	});

	it('marks a row short when the fridge holds less than needed', () => {
		const [row] = planDeduction([{ product: cream, amount: 330 }], [have(cream, 120)]);
		expect(row).toMatchObject({ needed: 330, available: 120, status: 'short', defaultOn: true });
	});

	it('marks a row missing when the product is absent or empty', () => {
		const rows = planDeduction(
			[
				{ product: flour, amount: 200 },
				{ product: cream, amount: 50 }
			],
			[have(cream, 0)]
		);
		expect(rows.map((r) => [r.status, r.available, r.defaultOn])).toEqual([
			['missing', 0, false],
			['missing', 0, false]
		]);
	});

	it('leaves out "do smaku" rows and sums a repeated product', () => {
		const rows = planDeduction(
			[
				{ product: salt, amount: null },
				{ product: flour, amount: 100 },
				{ product: flour, amount: 50 }
			],
			[have(flour, 500)]
		);
		expect(rows).toHaveLength(1);
		expect(rows[0]).toMatchObject({ product: flour, needed: 150, status: 'ok' });
	});

	it('works for count products in pieces', () => {
		const rows = planDeduction([{ product: egg, amount: 4 }], [have(egg, 6), have(egg, 1)]);
		expect(rows[0]).toMatchObject({ needed: 4, available: 7, status: 'ok' });
		const short = planDeduction([{ product: egg, amount: 4 }], [have(egg, 3)]);
		expect(short[0]?.status).toBe('short');
	});

	it('treats an exact match as ok despite floating point noise', () => {
		const [row] = planDeduction([{ product: flour, amount: 0.3 }], [have(flour, 0.1 + 0.2)]);
		expect(row?.status).toBe('ok');
	});
});

describe('neededFromIngredients', () => {
	it('scales base amounts and keeps "do smaku" as null', () => {
		const ingredient = (baseAmount: number | null): Ingredient =>
			({
				id: 'i',
				product: flour,
				quantity: null,
				baseAmount,
				note: null,
				optional: false
			}) as Ingredient;
		expect(neededFromIngredients([ingredient(100), ingredient(null)], 1.5)).toEqual([
			{ product: flour, amount: 150 },
			{ product: flour, amount: null }
		]);
	});
});

describe('selections and requests', () => {
	const rows = planDeduction(
		[
			{ product: cream, amount: 330 },
			{ product: flour, amount: 200 },
			{ product: egg, amount: 2 }
		],
		[have(cream, 120), have(egg, 6)]
	);

	it('switches on everything that is at least partly in the fridge', () => {
		expect([...defaultSelection(rows)]).toEqual(['cream', 'egg']);
		expect([...allAvailable(rows)]).toEqual(['cream', 'egg']);
	});

	it('builds lines for the selected rows only, never for missing ones', () => {
		const request = toDeductionRequest(rows, new Set(['cream', 'flour']), {
			recipeId: 'r1',
			variantId: 'v1'
		});
		expect(request).toEqual({
			recipeId: 'r1',
			variantId: 'v1',
			lines: [{ productId: 'cream', amount: 330 }]
		});
	});

	it('omits the variant without a recipe', () => {
		const request = toDeductionRequest(rows, new Set(['egg']), { variantId: 'v1' });
		expect(request).toEqual({ lines: [{ productId: 'egg', amount: 2 }] });
	});

	it('lists what to buy: shortfalls and missing products', () => {
		expect(toShoppingLines(rows)).toEqual([
			{ productId: 'cream', amount: 210 },
			{ productId: 'flour', amount: 200 }
		]);
	});
});
