// The entries of a unit picker (PROJECT §5.6): built-in units valid for the product, then its custom
// units, then "+ Nowa jednostka…". Values are a built-in unit name or `custom:<id>`.
import type { Option } from '$lib/components/ui/types';
import type { BuiltInUnit, CustomUnitRef, ProductRef, Unit } from '$lib/domain/types';
import { BUILT_IN_UNITS, customUnitFits, toBaseAmount } from '$lib/domain/units';
import { t } from '$lib/i18n';

export const NEW_UNIT_VALUE = 'new-unit';
const CUSTOM_PREFIX = 'custom:';

/** Built-in units that convert to the product's base kind (all of them without a product). */
export function compatibleBuiltIns(product: ProductRef | null): BuiltInUnit[] {
	if (!product) return [...BUILT_IN_UNITS];
	return BUILT_IN_UNITS.filter((unit) => toBaseAmount({ value: 1, unit }, product) !== null);
}

/** Custom units offered for the product: its own units and global units it converts to. */
export function compatibleCustomUnits(
	product: ProductRef | null,
	units: readonly CustomUnitRef[]
): CustomUnitRef[] {
	if (!product) return units.filter((u) => u.productId === null);
	return units.filter((u) => customUnitFits(u, product));
}

/** The picker value of a unit choice. */
export function unitValue(unit: Unit | null, customUnit: CustomUnitRef | null): string {
	if (unit === 'custom') return customUnit ? CUSTOM_PREFIX + customUnit.id : '';
	return unit ?? '';
}

/** The choice behind a picker value; null for "+ Nowa jednostka…" or an unknown value. */
export function parseUnitValue(
	value: string,
	units: readonly CustomUnitRef[]
): { unit: Unit; customUnit: CustomUnitRef | null } | null {
	if (value.startsWith(CUSTOM_PREFIX)) {
		const id = value.slice(CUSTOM_PREFIX.length);
		const customUnit = units.find((u) => u.id === id);
		return customUnit ? { unit: 'custom', customUnit } : null;
	}
	return (BUILT_IN_UNITS as readonly string[]).includes(value)
		? { unit: value as BuiltInUnit, customUnit: null }
		: null;
}

/** The picker entries, in order. `current` keeps a chosen custom unit listed even before loading. */
export function unitOptions(
	product: ProductRef | null,
	units: readonly CustomUnitRef[],
	current: CustomUnitRef | null,
	allowNew: boolean
): Option[] {
	const labels = t.recipes.ingredients.unitLabels;
	const custom = compatibleCustomUnits(product, units);
	if (current && !custom.some((u) => u.id === current.id)) custom.unshift(current);
	return [
		...compatibleBuiltIns(product).map((u) => ({ value: u, label: labels[u] })),
		...custom.map((u) => ({
			value: CUSTOM_PREFIX + u.id,
			label: u.name,
			...(u.productId !== null ? { hint: t.customUnits.productUnitHint } : {})
		})),
		...(allowNew ? [{ value: NEW_UNIT_VALUE, label: t.customUnits.newUnit }] : [])
	];
}
