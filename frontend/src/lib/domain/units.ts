// Unit constants and conversion to base amounts (PROJECT §5.1–5.2).
// Mirrors the backend QuantityConverter; both are checked against shared/test-vectors/quantities/.
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

/** Base amounts are rounded to this many decimals (the backend does the same). */
const BASE_DECIMALS = 4;

/** Rounds half away from zero; the nudge absorbs binary floating-point noise at exact ties. */
function roundBase(x: number): number {
	const f = 10 ** BASE_DECIMALS;
	return (Math.sign(x) * Math.round(Math.abs(x) * f + 1e-7)) / f;
}

type Kind = 'mass' | 'volume' | 'count';

const UNIT_NATIVE: Record<Exclude<Quantity['unit'], 'piece'>, [Kind, number]> = {
	g: ['mass', 1],
	kg: ['mass', 1000],
	ml: ['volume', ML_PER.ml],
	l: ['volume', ML_PER.l],
	glass: ['volume', ML_PER.glass],
	tbsp: ['volume', ML_PER.tbsp],
	tsp: ['volume', ML_PER.tsp],
	drop: ['volume', ML_PER.drop],
	pinch: ['volume', ML_PER.pinch]
};

function convert(amount: number, from: Kind, to: Kind, p: ProductRef): number | null {
	if (from === to) return amount;
	const density = p.gramsPerMl != null && p.gramsPerMl > 0 ? p.gramsPerMl : null;
	const pieceWeight = p.gramsPerPiece != null && p.gramsPerPiece > 0 ? p.gramsPerPiece : null;
	let grams: number | null;
	if (from === 'mass') grams = amount;
	else if (from === 'volume') grams = density === null ? null : amount * density;
	else grams = pieceWeight === null ? null : amount * pieceWeight;
	if (grams === null) return null;
	if (to === 'mass') return grams;
	if (to === 'volume') return density === null ? null : grams / density;
	return pieceWeight === null ? null : grams / pieceWeight;
}

/** Base amount (g, ml or medium pieces) of `q` for product `p`; null if the unit is incompatible. */
export function toBaseAmount(q: Quantity, p: ProductRef): number | null {
	let kind: Kind;
	let amount: number;
	if (q.unit === 'piece') {
		kind = 'count';
		// A size only counts for products that have sizes; otherwise every piece is a medium piece.
		amount = q.value * (p.hasSizes && q.size ? SIZE_FACTOR[q.size] : 1);
	} else {
		const [k, factor] = UNIT_NATIVE[q.unit];
		kind = k;
		amount = q.value * factor;
	}
	const converted = convert(amount, kind, p.measureKind, p);
	return converted === null ? null : roundBase(converted);
}
