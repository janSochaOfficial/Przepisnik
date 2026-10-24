// Grouping and searching fridge items on the client (the whole fridge is loaded at once).
import type { FridgeItem, ProductCategory } from '$lib/domain/types';

/** Catalog order of the categories (API §0 / PROJECT §7). */
export const CATEGORY_ORDER: ProductCategory[] = [
	'vegetables',
	'fruits',
	'meat',
	'fish',
	'dairy',
	'bakery',
	'dryGoods',
	'spices',
	'oilsVinegars',
	'sauces',
	'canned',
	'frozen',
	'drinks',
	'sweets',
	'other'
];

export type ItemGroup = { category: ProductCategory; items: FridgeItem[] };

/** Lower case without diacritics, so "maka" finds "mąka". */
export function normalizeSearch(text: string): string {
	return text
		.toLocaleLowerCase('pl')
		.replace(/ł/g, 'l')
		.normalize('NFD')
		.replace(/\p{M}/gu, '')
		.trim();
}

/** Items whose product name or note contains every word of `query`. */
export function filterItems(items: FridgeItem[], query: string): FridgeItem[] {
	const words = normalizeSearch(query).split(/\s+/).filter(Boolean);
	if (words.length === 0) return items;
	return items.filter((item) => {
		const haystack = normalizeSearch(`${item.product.name} ${item.note ?? ''}`);
		return words.every((w) => haystack.includes(w));
	});
}

/** Items grouped by product category in catalog order; empty groups are left out. Order inside a group is kept. */
export function groupByCategory(items: FridgeItem[]): ItemGroup[] {
	return CATEGORY_ORDER.flatMap((category) => {
		const inGroup = items.filter((i) => i.product.category === category);
		return inGroup.length > 0 ? [{ category, items: inGroup }] : [];
	});
}
