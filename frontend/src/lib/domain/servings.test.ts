import { describe, expect, it } from 'vitest';
import {
	clampServings,
	formatServings,
	formatServingsNumber,
	scaledServings,
	servingsFactor
} from './scaling';

// Servings (PROJECT §5.7, D-37).
describe('servingsFactor', () => {
	it('is chosen / recipe servings: 4 → 6 porcji is ×1.5', () => {
		expect(servingsFactor(4, 6)).toBe(1.5);
		expect(servingsFactor(4, 2)).toBe(0.5);
	});

	it.each([
		[null, 6],
		[undefined, 6],
		[0, 6],
		[4, 0]
	])('falls back to 1 for %s servings → %d', (recipe, chosen) => {
		expect(servingsFactor(recipe, chosen)).toBe(1);
	});
});

describe('clampServings', () => {
	it.each([
		[0, 1],
		[1, 1],
		[7.4, 7],
		[100, 100],
		[250, 100],
		[Number.NaN, 1]
	])('%d → %d', (n, want) => {
		expect(clampServings(n)).toBe(want);
	});
});

describe('scaledServings (scaled from an ingredient)', () => {
	it('anchor ×0.75 on 4 servings → 3 porcje', () => {
		const s = scaledServings(4, 0.75);
		expect(s).toEqual({ value: 3, approximate: false });
		expect(formatServings(s.value, s.approximate)).toBe('3 porcje');
	});

	it('rounds to ½ and marks values more than 5 % off: ×0.575 on 4 → ~2 ½ porcji', () => {
		const s = scaledServings(4, 0.575);
		expect(s).toEqual({ value: 2.5, approximate: true });
		expect(formatServings(s.value, s.approximate)).toBe('~2 ½ porcji');
	});

	it('×0.6 on 4 (2.4) shows 2 ½ porcji without ~, since 2 ½ is within 5 % (PROJECT §5.4)', () => {
		const s = scaledServings(4, 0.6);
		expect(s).toEqual({ value: 2.5, approximate: false });
		expect(formatServings(s.value, s.approximate)).toBe('2 ½ porcji');
	});

	it('never shows less than ½', () => {
		expect(scaledServings(1, 0.1)).toEqual({ value: 0.5, approximate: true });
	});
});

describe('formatServings (plural forms porcja / porcje / porcji / porcji)', () => {
	it.each([
		[1, '1 porcja'],
		[2, '2 porcje'],
		[4, '4 porcje'],
		[5, '5 porcji'],
		[12, '12 porcji'],
		[22, '22 porcje'],
		[0.5, '½ porcji'],
		[1.5, '1 ½ porcji']
	])('%d → %s', (n, want) => {
		expect(formatServings(n)).toBe(want);
	});

	it('prints the number alone with a ½ glyph', () => {
		expect(formatServingsNumber(6)).toBe('6');
		expect(formatServingsNumber(3.5)).toBe('3 ½');
	});
});
