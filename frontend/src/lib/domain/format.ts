// Display formatting of amounts (PROJECT §5.4). Implemented in F1; checked against shared/test-vectors/formatting/.
import { notImplemented } from './not-implemented';
import type { Ingredient, LiquidUnits, ProductRef, Quantity } from './types';

/** `inventory` ⇒ PROJECT §5.4 "Inventory format" (always metric for liquids and spices). */
export type FormatOptions = { liquidUnits: LiquidUnits; context?: 'recipe' | 'inventory' };

/** `text` never contains the `~` marker; `approximate` says whether to show it (> 5 % off). */
export type FormattedAmount = { text: string; approximate: boolean };

export function formatBaseAmount(
	_base: number,
	_p: ProductRef,
	_o: FormatOptions
): FormattedAmount {
	return notImplemented('formatBaseAmount');
}

export function formatQuantityAsEntered(_q: Quantity, _p: ProductRef): string {
	return notImplemented('formatQuantityAsEntered');
}

/** Null ⇒ "do smaku". */
export function formatIngredientAmount(
	_i: Ingredient,
	_factor: number,
	_o: FormatOptions
): FormattedAmount | null {
	return notImplemented('formatIngredientAmount');
}

export function formatNumberPl(_n: number, _maxFractionDigits?: number): string {
	return notImplemented('formatNumberPl');
}
