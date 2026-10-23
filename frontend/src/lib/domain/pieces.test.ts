import { describe, expect, it } from 'vitest';
import { bestPieceCombination } from './pieces';

const combo = (large: number, medium: number, small: number) => ({ large, medium, small });

describe('bestPieceCombination', () => {
	it('uses a quarter of a medium below 0.3', () => {
		expect(bestPieceCombination(0.29)).toEqual({ fractionOfMedium: 0.25 });
		expect(bestPieceCombination(0)).toEqual({ fractionOfMedium: 0.25 });
	});

	it('uses half of a medium from 0.3 up to 0.625', () => {
		expect(bestPieceCombination(0.3)).toEqual({ fractionOfMedium: 0.5 });
		expect(bestPieceCombination(0.62)).toEqual({ fractionOfMedium: 0.5 });
	});

	it('rounds 2.8 medium onions to 2 large', () => {
		expect(bestPieceCombination(2.8)).toEqual(combo(2, 0, 0));
	});

	it('rounds 1.4 to 1 large', () => {
		expect(bestPieceCombination(1.4)).toEqual(combo(1, 0, 0));
	});

	it('prefers the smaller error among the fewest pieces (3.5 is 2 large + 1 medium)', () => {
		expect(bestPieceCombination(3.5)).toEqual(combo(2, 1, 0));
	});

	it('picks one medium for exactly 1', () => {
		expect(bestPieceCombination(1)).toEqual(combo(0, 1, 0));
	});

	it('prefers more medium pieces on an error tie', () => {
		// 2 medium = 2.0 and 1 large + 1 small = 2.05 are both 0.025 away from 2.025.
		expect(bestPieceCombination(2.025)).toEqual(combo(0, 2, 0));
	});

	it('falls back to the smallest error when nothing is within 10 %', () => {
		expect(bestPieceCombination(0.625)).toEqual(combo(0, 0, 1));
	});

	it('handles large targets', () => {
		expect(bestPieceCombination(10)).toEqual(combo(7, 0, 0));
	});
});
