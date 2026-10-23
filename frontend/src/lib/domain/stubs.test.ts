import { describe, expect, it } from 'vitest';
import { ML_PER, SIZE_FACTOR } from './units';

// The stubs throw until F1/F2 implement them; only the constants are fixed by T00 (PROJECT §5.2).
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
