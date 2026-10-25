import { describe, expect, it } from 'vitest';
import type { CustomUnitRef, ProductRef } from '$lib/domain/types';
import { baseUnitOf, convertDraft, draftOf, draftQuantity, roundForUnit } from './quantity';

const product = (over: Partial<ProductRef>): ProductRef =>
	({
		id: 'p',
		name: 'p',
		measureKind: 'volume',
		displayClass: 'liquid',
		hasSizes: false,
		...over
	}) as ProductRef;

const milk = product({ gramsPerMl: 1.03 });
const tomatoes = product({ id: 't', measureKind: 'mass', displayClass: 'weight' });
const onion = product({ id: 'o', measureKind: 'count', displayClass: 'piece', hasSizes: true });
const can = {
	id: 'c',
	name: 'puszka',
	forms: { one: 'puszka', few: 'puszki', many: 'puszek', fraction: 'puszki' },
	productId: 't',
	measureKind: 'mass',
	amount: 400
} as CustomUnitRef;

type Unit = Parameters<typeof draftQuantity>[0]['unit'];
const draft = (value: number | null, unit: Unit, extra = {}) => ({
	value,
	unit,
	customUnit: null,
	size: null,
	...extra
});

describe('convertDraft', () => {
	it('converts litres to millilitres and glasses', () => {
		expect(convertDraft(draft(1.5, 'l'), draft(null, 'ml'), milk)).toBe(1500);
		expect(convertDraft(draft(1500, 'ml'), draft(null, 'glass'), milk)).toBe(6);
	});

	it('rounds to a quarter for glasses and custom units, 2 decimals for metric units', () => {
		expect(convertDraft(draft(100, 'ml'), draft(null, 'glass'), milk)).toBe(0.5);
		expect(convertDraft(draft(333, 'ml'), draft(null, 'l'), milk)).toBe(0.33);
		const target = draft(null, 'custom', { customUnit: can });
		expect(convertDraft(draft(500, 'g'), target, tomatoes)).toBe(1.25);
	});

	it('converts from a custom unit', () => {
		const from = draft(2, 'custom', { customUnit: can });
		expect(convertDraft(from, draft(null, 'g'), tomatoes)).toBe(800);
	});

	it('returns null for an empty value or a unit that does not convert', () => {
		expect(convertDraft(draft(null, 'ml'), draft(null, 'l'), milk)).toBeNull();
		expect(convertDraft(draft(1, 'kg'), draft(null, 'l'), tomatoes)).toBeNull();
	});

	it('keeps the piece size when converting between pieces', () => {
		const large = { size: 'large' as const };
		expect(convertDraft(draft(2, 'piece', large), draft(null, 'piece', large), onion)).toBe(2);
	});
});

describe('draftOf and draftQuantity', () => {
	it('starts from the base amount in the base unit when there is no quantity', () => {
		expect(baseUnitOf(milk)).toBe('ml');
		expect(draftOf({ product: milk, amount: 300, quantity: null } as never)).toEqual(
			draft(300, 'ml')
		);
	});

	it('sends only writable fields', () => {
		expect(draftQuantity(draft(2, 'custom', { customUnit: can }), tomatoes)).toEqual({
			value: 2,
			unit: 'custom',
			customUnitId: 'c'
		});
		expect(draftQuantity(draft(2, 'piece', { size: 'large' }), onion)).toEqual({
			value: 2,
			unit: 'piece',
			size: 'large'
		});
		expect(draftQuantity(draft(0, 'g'), tomatoes)).toBeNull();
	});

	it('rounds to at least a quarter', () => {
		expect(roundForUnit(0.1, 'tsp')).toBe(0.25);
	});
});
