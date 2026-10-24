// Unit constants and conversion to base amounts (PROJECT §5.1–5.2, §5.6).
// Mirrors the backend QuantityConverter; both are checked against shared/test-vectors/quantities/.
import type {
	BuiltInUnit,
	CustomUnitRef,
	MeasureKind,
	PieceSize,
	ProductRef,
	Quantity
} from './types';

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

/** The built-in units, in the order unit pickers list them. */
export const BUILT_IN_UNITS: readonly BuiltInUnit[] = [
	'g',
	'kg',
	'ml',
	'l',
	'glass',
	'tbsp',
	'tsp',
	'drop',
	'pinch',
	'piece'
];

/** Base amounts are rounded to this many decimals (the backend does the same). */
const BASE_DECIMALS = 4;

/** Rounds half away from zero; the nudge absorbs binary floating-point noise at exact ties. */
export function roundBase(x: number): number {
	const f = 10 ** BASE_DECIMALS;
	return (Math.sign(x) * Math.round(Math.abs(x) * f + 1e-7)) / f;
}

const UNIT_NATIVE: Record<Exclude<BuiltInUnit, 'piece'>, [MeasureKind, number]> = {
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

/**
 * Converts an amount between measure kinds with the product's density (mass ↔ volume) and piece
 * weight (count ↔ mass); null when the product lacks the data.
 */
export function convertKind(
	amount: number,
	from: MeasureKind,
	to: MeasureKind,
	p: Pick<ProductRef, 'gramsPerMl' | 'gramsPerPiece'>
): number | null {
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

/**
 * A built-in quantity in its unit's own base unit (g, ml or medium pieces). A size counts only when
 * `useSizes`. Null for a custom unit, which has no kind of its own.
 */
export function nativeAmount(q: Quantity, useSizes: boolean): [MeasureKind, number] | null {
	if (q.unit === 'custom') return null;
	if (q.unit === 'piece')
		return ['count', q.value * (useSizes && q.size ? SIZE_FACTOR[q.size] : 1)];
	const [kind, factor] = UNIT_NATIVE[q.unit];
	return [kind, q.value * factor];
}

/** Whether a custom unit may be used for `p`: a product unit only for its own product. */
export function unitAllowedFor(unit: CustomUnitRef, p: Pick<ProductRef, 'id'>): boolean {
	return unit.productId == null || unit.productId === p.id;
}

/**
 * Base amount (g, ml or medium pieces) of `q` for product `p`; null if the unit is incompatible.
 * A custom quantity uses `unit`, or the `customUnit` embedded in it (PROJECT §5.6).
 */
export function toBaseAmount(
	q: Quantity,
	p: ProductRef,
	unit?: CustomUnitRef | null
): number | null {
	let native: [MeasureKind, number] | null;
	if (q.unit === 'custom') {
		const u = unit ?? q.customUnit ?? null;
		native =
			u && (q.customUnitId == null || u.id === q.customUnitId) && unitAllowedFor(u, p)
				? [u.measureKind, q.value * u.amount]
				: null;
	} else {
		native = nativeAmount(q, p.hasSizes);
	}
	if (native === null) return null;
	const converted = convertKind(native[1], native[0], p.measureKind, p);
	return converted === null ? null : roundBase(converted);
}

/** Whether `unit` converts to `p`'s measure kind (so pickers offer it). */
export function customUnitFits(unit: CustomUnitRef, p: ProductRef): boolean {
	return toBaseAmount({ value: 1, unit: 'custom', customUnitId: unit.id }, p, unit) !== null;
}
