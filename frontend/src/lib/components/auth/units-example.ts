import { formatBaseAmount } from '$lib/domain/format';
import type { LiquidUnits, ProductRef } from '$lib/domain/types';

/** 190 ml of milk: "¾ szklanki" in kitchen units, "190 ml" in metric. */
const EXAMPLE_BASE_ML = 190;
const MILK = { id: 'example', name: 'mleko', displayClass: 'liquid' } as unknown as ProductRef;

/** How the profile's example amount reads under the given liquid-unit setting. */
export function liquidExample(liquidUnits: LiquidUnits): string {
	return formatBaseAmount(EXAMPLE_BASE_ML, MILK, { liquidUnits }).text;
}
