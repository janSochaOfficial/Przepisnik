// @vitest-environment node
import { readdirSync, readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import type { CustomUnitRef, ProductRef, Quantity } from './types';
import { customUnitFits, ML_PER, SIZE_FACTOR, toBaseAmount, unitAllowedFor } from './units';

type Case = {
	id: string;
	quantity: Quantity;
	product: Pick<ProductRef, 'measureKind' | 'hasSizes' | 'gramsPerMl' | 'gramsPerPiece'>;
	customUnit?: VectorUnit;
	expected: number | null;
};

/** `scope`: a unit of the case's product, of another product, or for every product. */
type VectorUnit = {
	measureKind: CustomUnitRef['measureKind'];
	amount: number;
	scope: 'product' | 'otherProduct' | 'all';
};

const PRODUCT_ID = 'p';

function unitRef(u: VectorUnit): CustomUnitRef {
	return {
		id: 'u',
		name: 'u',
		forms: { one: 'u', few: 'u', many: 'u', fraction: 'u' },
		productId: u.scope === 'product' ? PRODUCT_ID : u.scope === 'otherProduct' ? 'other' : null,
		measureKind: u.measureKind,
		amount: u.amount
	};
}

const dir = fileURLToPath(new URL('../../../../shared/test-vectors/quantities/', import.meta.url));
const cases: Case[] = readdirSync(dir)
	.filter((f) => f.endsWith('.json'))
	.flatMap((f) => (JSON.parse(readFileSync(dir + f, 'utf8')) as { cases: Case[] }).cases);

const asProduct = (p: Case['product']) => ({ id: PRODUCT_ID, ...p }) as ProductRef;

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
		const unit = c.customUnit ? unitRef(c.customUnit) : null;
		const quantity: Quantity = unit ? { ...c.quantity, customUnitId: unit.id } : c.quantity;
		const actual = toBaseAmount(quantity, asProduct(c.product), unit);
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

	// The vector schema forbids zero density / piece weight, so the guards are tested here.
	it('treats a zero density as missing', () => {
		const p = asProduct({
			measureKind: 'mass',
			hasSizes: false,
			gramsPerMl: 0,
			gramsPerPiece: null
		});
		expect(toBaseAmount({ value: 1, unit: 'tbsp' }, p)).toBeNull();
	});

	it('treats a zero piece weight as missing', () => {
		const p = asProduct({
			measureKind: 'count',
			hasSizes: false,
			gramsPerMl: null,
			gramsPerPiece: 0
		});
		expect(toBaseAmount({ value: 100, unit: 'g' }, p)).toBeNull();
	});
});

describe('custom units', () => {
	const flour = asProduct({ measureKind: 'mass', hasSizes: false, gramsPerMl: 0.53, gramsPerPiece: null });
	const can: CustomUnitRef = {
		id: 'can',
		name: 'puszka',
		forms: { one: 'puszka', few: 'puszki', many: 'puszek', fraction: 'puszki' },
		productId: PRODUCT_ID,
		measureKind: 'mass',
		amount: 400
	};

	it('uses the unit embedded in the quantity', () => {
		expect(toBaseAmount({ value: 2, unit: 'custom', customUnitId: 'can', customUnit: can }, flour)).toBe(800);
	});

	it('is null without a unit or for a different unit id', () => {
		expect(toBaseAmount({ value: 2, unit: 'custom', customUnitId: 'can' }, flour)).toBeNull();
		expect(toBaseAmount({ value: 2, unit: 'custom', customUnitId: 'x' }, flour, can)).toBeNull();
	});

	it('offers a product unit only for its product', () => {
		expect(unitAllowedFor(can, flour)).toBe(true);
		expect(unitAllowedFor(can, { id: 'other' })).toBe(false);
		expect(customUnitFits(can, { ...flour, id: 'other' })).toBe(false);
	});

	it('offers a volume unit for a mass product only with a density', () => {
		const cup = { ...can, id: 'cup', productId: null, measureKind: 'volume' as const, amount: 150 };
		expect(customUnitFits(cup, flour)).toBe(true);
		expect(customUnitFits(cup, { ...flour, gramsPerMl: null })).toBe(false);
	});
});
