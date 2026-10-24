import { describe, expect, it } from 'vitest';
import type { FridgeItem, ProductCategory } from '$lib/domain/types';
import { filterItems, groupByCategory, normalizeSearch } from './group';

const item = (name: string, category: ProductCategory, note: string | null = null): FridgeItem =>
	({ id: name, product: { name, category }, note }) as FridgeItem;

describe('groupByCategory', () => {
	it('orders groups like the catalog and keeps the order inside a group', () => {
		const groups = groupByCategory([
			item('sól', 'spices'),
			item('mleko', 'dairy'),
			item('marchew', 'vegetables'),
			item('ser', 'dairy')
		]);
		expect(groups.map((g) => g.category)).toEqual(['vegetables', 'dairy', 'spices']);
		expect(groups[1]?.items.map((i) => i.id)).toEqual(['mleko', 'ser']);
	});

	it('returns nothing for an empty fridge', () => {
		expect(groupByCategory([])).toEqual([]);
	});
});

describe('filterItems', () => {
	const items = [item('mąka pszenna', 'dryGoods'), item('mleko', 'dairy', 'otwarte w niedzielę')];

	it('ignores case and diacritics', () => {
		expect(normalizeSearch('  Łódź ')).toBe('lodz');
		expect(filterItems(items, 'MAKA').map((i) => i.id)).toEqual(['mąka pszenna']);
	});

	it('requires every word, and also searches the note', () => {
		expect(filterItems(items, 'mleko niedziele').map((i) => i.id)).toEqual(['mleko']);
		expect(filterItems(items, 'mleko mąka')).toEqual([]);
	});

	it('returns everything for a blank query', () => {
		expect(filterItems(items, '  ')).toHaveLength(2);
	});
});
