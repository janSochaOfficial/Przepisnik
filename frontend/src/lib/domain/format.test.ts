// @vitest-environment node
import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
	formatBaseAmount,
	formatIngredientAmount,
	formatNumberPl,
	formatQuantityAsEntered,
	type FormatOptions
} from './format';
import type { Ingredient, ProductRef, Quantity } from './types';

type Case = {
	id: string;
	base: number;
	product: Pick<
		ProductRef,
		'measureKind' | 'displayClass' | 'hasSizes' | 'allowHalf' | 'pieceForms'
	>;
	options: FormatOptions;
	expected: { text: string; approximate: boolean };
};

const dir = fileURLToPath(new URL('../../../../shared/test-vectors/formatting/', import.meta.url));
const cases: Case[] = readdirSync(dir)
	.filter((f) => f.endsWith('.json'))
	.flatMap((f) => (JSON.parse(readFileSync(dir + f, 'utf8')) as { cases: Case[] }).cases);

const ref = (p: Partial<ProductRef>) => ({ id: 'p', name: 'x', ...p }) as ProductRef;

describe('shared formatting vectors', () => {
	it('have unique ids and enough F1 cases', () => {
		const ids = cases.map((c) => c.id);
		expect(new Set(ids).size).toBe(ids.length);
		expect(ids.filter((id) => id.startsWith('F1-')).length).toBeGreaterThanOrEqual(20);
	});

	it.each(cases.map((c) => [c.id, c] as const))('formatBaseAmount passes %s', (_id, c) => {
		expect(formatBaseAmount(c.base, c.product as ProductRef, c.options)).toEqual(c.expected);
	});
});

describe('formatBaseAmount edge cases', () => {
	const kitchen: FormatOptions = { liquidUnits: 'kitchen' };

	it('shows zero without a rounding marker', () => {
		expect(formatBaseAmount(0, ref({ displayClass: 'weight' }), kitchen)).toEqual({
			text: '0 g',
			approximate: false
		});
		expect(formatBaseAmount(0, ref({ displayClass: 'liquid' }), kitchen).text).toBe('0 ml');
		expect(formatBaseAmount(0, ref({ displayClass: 'piece' }), kitchen).text).toBe('0 szt.');
	});

	it('defaults the context to recipe', () => {
		const liquid = ref({ displayClass: 'liquid' });
		expect(formatBaseAmount(250, liquid, kitchen).text).toBe('1 szklanka');
	});
});

describe('formatNumberPl', () => {
	it('uses a decimal comma and no grouping', () => {
		expect(formatNumberPl(1.25)).toBe('1,25');
		expect(formatNumberPl(12000)).toBe('12000');
	});

	it('limits the fraction digits', () => {
		expect(formatNumberPl(1.23456)).toBe('1,23');
		expect(formatNumberPl(1.23456, 3)).toBe('1,235');
		expect(formatNumberPl(2.5, 0)).toBe('3');
	});

	it('never prints negative zero', () => {
		expect(formatNumberPl(-0.001, 1)).toBe('0');
	});
});

describe('formatQuantityAsEntered', () => {
	const flour = ref({ measureKind: 'mass', displayClass: 'weight' });
	const milk = ref({ measureKind: 'volume', displayClass: 'liquid' });
	const onion = ref({ measureKind: 'count', displayClass: 'piece', hasSizes: true });
	const egg = ref({
		measureKind: 'count',
		displayClass: 'piece',
		pieceForms: { one: 'jajko', few: 'jajka', many: 'jajek', fraction: 'jajka' }
	});
	const as = (value: number, unit: Quantity['unit'], p: ProductRef, size?: Quantity['size']) =>
		formatQuantityAsEntered({ value, unit, size }, p);

	it('keeps metric units as is', () => {
		expect(as(200, 'g', flour)).toBe('200 g');
		expect(as(1.5, 'kg', flour)).toBe('1 ½ kg');
		expect(as(330, 'ml', milk)).toBe('330 ml');
		expect(as(0.75, 'l', milk)).toBe('¾ l');
		expect(as(1.3, 'l', milk)).toBe('1,3 l');
	});

	it('inflects spoon and glass units', () => {
		expect(as(1, 'glass', milk)).toBe('1 szklanka');
		expect(as(1.5, 'glass', milk)).toBe('1 ½ szklanki');
		expect(as(2, 'glass', milk)).toBe('2 szklanki');
		expect(as(5, 'glass', milk)).toBe('5 szklanek');
		expect(as(12, 'glass', milk)).toBe('12 szklanek');
		expect(as(22, 'glass', milk)).toBe('22 szklanki');
		expect(as(3, 'tbsp', milk)).toBe('3 łyżki');
		expect(as(0.5, 'tsp', milk)).toBe('½ łyżeczki');
		expect(as(10, 'drop', milk)).toBe('10 kropli');
		expect(as(2, 'pinch', milk)).toBe('2 szczypty');
	});

	it('uses the size word for sized products and the piece forms otherwise', () => {
		expect(as(2, 'piece', onion, 'large')).toBe('2 duże');
		expect(as(1, 'piece', onion, 'small')).toBe('1 mała');
		expect(as(5, 'piece', onion, 'medium')).toBe('5 średnich');
		expect(as(2, 'piece', onion)).toBe('2 szt.');
		expect(as(5, 'piece', egg)).toBe('5 jajek');
		expect(as(2, 'piece', egg, 'large')).toBe('2 jajka');
	});
});

describe('formatIngredientAmount', () => {
	const kitchen: FormatOptions = { liquidUnits: 'kitchen' };
	const flour = ref({ measureKind: 'mass', displayClass: 'weight' });
	const ingredient = (quantity: Quantity | null, baseAmount: number | null): Ingredient => ({
		id: 'i',
		product: flour,
		quantity,
		baseAmount,
		note: null,
		optional: false
	});

	it('returns null for "do smaku"', () => {
		expect(formatIngredientAmount(ingredient(null, null), 2, kitchen)).toBeNull();
		expect(formatIngredientAmount(ingredient(null, null), 1, kitchen)).toBeNull();
	});

	it('shows the amount as entered at factor 1', () => {
		const i = ingredient({ value: 47, unit: 'g' }, 47);
		expect(formatIngredientAmount(i, 1, kitchen)).toEqual({ text: '47 g', approximate: false });
	});

	it('formats the scaled base amount with the display rules', () => {
		const i = ingredient({ value: 47, unit: 'g' }, 47);
		expect(formatIngredientAmount(i, 2, kitchen)).toEqual({ text: '95 g', approximate: false });
		expect(formatIngredientAmount(i, 1.5, kitchen)).toEqual({ text: '70 g', approximate: false });
	});

	it('scales the entered value when there is no base amount', () => {
		const i = ingredient({ value: 2, unit: 'tbsp' }, null);
		expect(formatIngredientAmount(i, 2, kitchen)).toEqual({ text: '4 łyżki', approximate: false });
	});
});
