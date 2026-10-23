// Scaling from "Mam: …" (PROJECT §5.3).
import type { Ingredient } from './types';

/** entered base amount / recipe base amount; 1 when the anchor is unusable (zero, negative or not finite). */
export function scaleFactor(anchorRecipeBase: number, anchorUserBase: number): number {
	if (!(anchorRecipeBase > 0) || !Number.isFinite(anchorUserBase) || !(anchorUserBase > 0)) {
		return 1;
	}
	return anchorUserBase / anchorRecipeBase;
}

/** Multiplies every ingredient with a quantity by `factor`; "do smaku" ingredients keep `null`. */
export function scaleIngredients(
	list: Ingredient[],
	factor: number
): (Ingredient & { scaledBaseAmount: number | null })[] {
	return list.map((i) => ({
		...i,
		scaledBaseAmount: i.quantity === null || i.baseAmount === null ? null : i.baseAmount * factor
	}));
}
