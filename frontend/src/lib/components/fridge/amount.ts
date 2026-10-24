// Shared helpers for amounts in the fridge, the shopping list and the deduction dialog.
import { formatBaseAmount } from '$lib/domain/format';
import type { LiquidUnits, ProductRef } from '$lib/domain/types';
import { t } from '$lib/i18n';
import { units } from '$lib/i18n/pl/units';

/** The user's liquid-unit setting from the layout data (`kitchen` until `/me` is known). */
export function liquidUnitsOf(data: { me?: { settings: { liquidUnits: LiquidUnits } } | null }) {
	return data.me?.settings.liquidUnits ?? 'kitchen';
}

/** The default fridge id from the layout data, if the user picked one. */
export function defaultFridgeIdOf(data: {
	me?: { settings: { defaultFridgeId?: string | null } } | null;
}): string | null {
	return data.me?.settings.defaultFridgeId ?? null;
}

/** `~230 ml`: an amount in the inventory format, with `~` when the shown value is rounded. */
export function inventoryText(amount: number, product: ProductRef, liquidUnits: LiquidUnits) {
	const { text, approximate } = formatBaseAmount(amount, product, {
		liquidUnits,
		context: 'inventory'
	});
	return approximate ? `~${text}` : text;
}

/** The base unit a product is entered in: g, ml or pieces. */
export function baseUnitLabel(product: ProductRef): string {
	if (product.displayClass === 'weight') return units.g;
	if (product.displayClass === 'piece') return units.piece.many;
	return units.ml;
}

/** `Śmietanka 30%` style line parts for a fridge item (PROJECT §8.2). */
export function remainingText(
	item: { remainingPercent: number; remainingAmount: number; product: ProductRef },
	liquidUnits: LiquidUnits
): string {
	return t.fridge.item.remaining(
		t.common.ui.percent(
			new Intl.NumberFormat('pl-PL', { maximumFractionDigits: 2 }).format(item.remainingPercent)
		),
		inventoryText(item.remainingAmount, item.product, liquidUnits)
	);
}
