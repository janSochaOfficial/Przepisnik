import { describe, expect, it } from 'vitest';
import { normalizeProductName, sameProductName } from './product-name';

describe('product names', () => {
	it('normalizes case, diacritics, ł and spacing like the server', () => {
		expect(normalizeProductName('  Śmietana   18% ')).toBe('smietana 18%');
		expect(normalizeProductName('Łosoś')).toBe('losos');
	});

	it('compares normalized names', () => {
		expect(sameProductName('Cebula', 'cebula ')).toBe(true);
		expect(sameProductName('maka', 'Mąka')).toBe(true);
		expect(sameProductName('cebula', 'cebulka')).toBe(false);
	});
});
