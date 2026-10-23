// Unit constants and conversion to base amounts (PROJECT §5.1–5.2). Implemented in F1.
// Must mirror the backend QuantityConverter; both are checked against shared/test-vectors/quantities/.
import { notImplemented } from './not-implemented';
import type { PieceSize, ProductRef, Quantity } from './types';

export const ML_PER: Record<'glass' | 'tbsp' | 'tsp' | 'drop' | 'pinch' | 'ml' | 'l', number> = {
	glass: 250,
	tbsp: 15,
	tsp: 5,
	drop: 0.05,
	pinch: 0.3,
	ml: 1,
	l: 1000
};

export const SIZE_FACTOR: Record<PieceSize, number> = { small: 0.75, medium: 1, large: 1.3 };

/** Base amount (g, ml or medium pieces) of `q` for product `p`; null if the unit is incompatible. */
export function toBaseAmount(_q: Quantity, _p: ProductRef): number | null {
	return notImplemented('toBaseAmount');
}
