// @vitest-environment node
import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import type { ProductRef, Quantity } from './types';
import { ML_PER, SIZE_FACTOR, toBaseAmount } from './units';

type Case = {
	id: string;
	quantity: Quantity;
	product: Pick<ProductRef, 'measureKind' | 'hasSizes' | 'gramsPerMl' | 'gramsPerPiece'>;
	expected: number | null;
};

const dir = fileURLToPath(new URL('../../../../shared/test-vectors/quantities/', import.meta.url));
const cases: Case[] = readdirSync(dir)
	.filter((f) => f.endsWith('.json'))
	.flatMap((f) => (JSON.parse(readFileSync(dir + f, 'utf8')) as { cases: Case[] }).cases);

const asProduct = (p: Case['product']) => p as ProductRef;

describe('units constants', () => {
	it('match the fixed conversion constants (D-07)', () => {
		expect(ML_PER).toEqual({
			glass: 250,
			tbsp: 15,
			tsp: 5,
			drop: 0.05,
			pinch: 0.3,
			ml: 1,
			l: 1000
		});
		expect(SIZE_FACTOR).toEqual({ small: 0.75, medium: 1, large: 1.3 });
	});
});

describe('shared quantity vectors', () => {
	it('have unique ids', () => {
		const ids = cases.map((c) => c.id);
		expect(new Set(ids).size).toBe(ids.length);
		expect(cases.length).toBeGreaterThan(30);
	});

	it.each(cases.map((c) => [c.id, c] as const))('toBaseAmount passes %s', (_id, c) => {
		const actual = toBaseAmount(c.quantity, asProduct(c.product));
		if (c.expected === null) expect(actual).toBeNull();
		else expect(actual).toBeCloseTo(c.expected, 4);
	});
});

describe('toBaseAmount', () => {
	const mass = asProduct({
		measureKind: 'mass',
		hasSizes: false,
		gramsPerMl: null,
		gramsPerPiece: null
	});

	it('converts kilograms to grams', () => {
		expect(toBaseAmount({ value: 2, unit: 'kg' }, mass)).toBe(2000);
	});

	it('rounds to 4 decimals, half away from zero', () => {
		const p = asProduct({
			measureKind: 'volume',
			hasSizes: false,
			gramsPerMl: 3,
			gramsPerPiece: null
		});
		expect(toBaseAmount({ value: 2, unit: 'g' }, p)).toBe(0.6667);
	});

	it('returns null when the unit cannot be converted', () => {
		expect(toBaseAmount({ value: 1, unit: 'glass' }, mass)).toBeNull();
	});
});
