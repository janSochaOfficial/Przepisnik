// Scaling from "Mam: …" (PROJECT §5.3). Implemented in F1.
import { notImplemented } from './not-implemented';
import type { Ingredient } from './types';

export function scaleFactor(_anchorRecipeBase: number, _anchorUserBase: number): number {
	return notImplemented('scaleFactor');
}

export function scaleIngredients(
	_list: Ingredient[],
	_factor: number
): (Ingredient & { scaledBaseAmount: number | null })[] {
	return notImplemented('scaleIngredients');
}
