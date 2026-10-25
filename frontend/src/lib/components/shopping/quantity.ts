// Quantities of shopping-list items (PROJECT §10, D-34): the value the dialogs edit, the unit it is
// in, and conversion of the value when the unit changes.
import type {
	BuiltInUnit,
	CustomUnitRef,
	PieceSize,
	ProductRef,
	Quantity,
	ShoppingListItem,
	Unit
} from '$lib/domain/types';
import { toBaseAmount } from '$lib/domain/units';

/** The value and unit of a quantity field. */
export type QuantityDraft = {
	value: number | null;
	unit: Unit;
	customUnit: CustomUnitRef | null;
	size: PieceSize | null;
};

/** The built-in unit a base amount of `product` is in. */
export function baseUnitOf(product: ProductRef): BuiltInUnit {
	if (product.measureKind === 'mass') return 'g';
	if (product.measureKind === 'count') return 'piece';
	return 'ml';
}

/** What the edit dialog starts with: the chosen quantity, else the base amount in its base unit. */
export function draftOf(item: ShoppingListItem): QuantityDraft {
	if (item.quantity) {
		return {
			value: item.quantity.value,
			unit: item.quantity.unit,
			customUnit: item.quantity.customUnit ?? null,
			size: item.quantity.size ?? null
		};
	}
	return {
		value: item.amount ?? null,
		unit: item.product ? baseUnitOf(item.product) : 'g',
		customUnit: null,
		size: null
	};
}

function quantityOf(
	value: number,
	unit: Unit,
	customUnit: CustomUnitRef | null,
	size: PieceSize | null,
	product: ProductRef
): Quantity {
	return {
		value,
		unit,
		...(unit === 'piece' && size && product.hasSizes ? { size } : {}),
		...(unit === 'custom' && customUnit ? { customUnitId: customUnit.id } : {})
	};
}

/** The quantity to send for a draft with a value above zero; null when there is none. */
export function draftQuantity(draft: QuantityDraft, product: ProductRef): Quantity | null {
	if (draft.value === null || !(draft.value > 0)) return null;
	return quantityOf(draft.value, draft.unit, draft.customUnit, draft.size, product);
}

const QUARTER_UNITS: readonly Unit[] = ['glass', 'tbsp', 'tsp', 'piece', 'custom'];

/** ¼ for glass/tbsp/tsp/piece/custom units (at least ¼), 2 decimals for the rest. */
export function roundForUnit(value: number, unit: Unit): number {
	if (QUARTER_UNITS.includes(unit)) return Math.max(0.25, Math.round(value * 4) / 4);
	return Math.round(value * 100) / 100;
}

/**
 * The value of `from` expressed in the unit of `to` for `product`, rounded for that unit; null when
 * the value is empty or a unit does not convert (the caller then keeps the number).
 */
export function convertDraft(
	from: QuantityDraft,
	to: Pick<QuantityDraft, 'unit' | 'customUnit' | 'size'>,
	product: ProductRef
): number | null {
	if (from.value === null || !(from.value > 0)) return null;
	const base = toBaseAmount(
		quantityOf(from.value, from.unit, from.customUnit, from.size, product),
		product,
		from.customUnit
	);
	const one = toBaseAmount(
		quantityOf(1, to.unit, to.customUnit, to.size, product),
		product,
		to.customUnit
	);
	if (base === null || one === null || !(one > 0)) return null;
	return roundForUnit(base / one, to.unit);
}
