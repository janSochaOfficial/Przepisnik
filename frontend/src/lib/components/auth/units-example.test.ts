import { describe, expect, it } from 'vitest';
import { liquidExample } from './units-example';

describe('liquidExample', () => {
	it('reads as glasses in kitchen units and as millilitres in metric', () => {
		expect(liquidExample('kitchen')).toBe('¾ szklanki');
		expect(liquidExample('metric')).toBe('190 ml');
	});
});
