// The plain-text form of a shopping list for Google Keep (PROJECT §10): a title line, a blank line,
// then one unchecked item per line.
import { t } from '$lib/i18n';
import { formatBaseAmount } from './format';
import type { LiquidUnits, ShoppingListItem } from './types';

function capitalise(text: string): string {
	return text.charAt(0).toLocaleUpperCase('pl-PL') + text.slice(1);
}

/** `Mleko — 1 l`, `Mleko` without an amount, or the free text as typed. */
export function shoppingLine(item: ShoppingListItem, liquidUnits: LiquidUnits): string {
	if (!item.product) return (item.text ?? '').trim();
	const name = capitalise(item.product.name);
	if (item.amount === null || !(item.amount > 0)) return name;
	const { text, approximate } = formatBaseAmount(item.amount, item.product, {
		liquidUnits,
		context: 'inventory'
	});
	return `${name} — ${approximate ? '~' : ''}${text}`;
}

/** Unchecked items only, in list order. */
export function shoppingListText(
	fridgeName: string,
	items: ShoppingListItem[],
	liquidUnits: LiquidUnits
): string {
	const lines = items.filter((i) => !i.checked).map((i) => shoppingLine(i, liquidUnits));
	return [t.shopping.shareTitle(fridgeName), '', ...lines].join('\n');
}
