// Scaling from "Mam: …" (PROJECT §5.3) and from servings (PROJECT §5.7, D-37).
import { servingsForms } from '$lib/i18n/pl/recipes';
import { pluralCategory } from './plural';
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

export const SERVINGS_MIN = 1;
export const SERVINGS_MAX = 100;

const APPROXIMATE_ABOVE = 0.05;
const EPS = 1e-9;

/** Whole servings within 1–100: fractions are rounded, anything outside is clamped. */
export function clampServings(n: number): number {
	if (!Number.isFinite(n)) return SERVINGS_MIN;
	return Math.min(SERVINGS_MAX, Math.max(SERVINGS_MIN, Math.round(n)));
}

/** chosen / recipe servings, the factor of a servings choice; 1 when either is unusable. */
export function servingsFactor(recipeServings: number | null | undefined, chosen: number): number {
	if (recipeServings == null || !(recipeServings > 0) || !(chosen > 0)) return 1;
	return chosen / recipeServings;
}

export type ScaledServings = { value: number; approximate: boolean };

/**
 * The servings line when the recipe is scaled from an ingredient: `servings × factor` rounded to ½
 * (at least ½), approximate when the shown value is more than 5 % off the exact one (§5.4).
 */
export function scaledServings(servings: number, factor: number): ScaledServings {
	const exact = servings * factor;
	const value = Math.max(0.5, Math.round(exact * 2 + EPS) / 2);
	const approximate = exact > 0 && Math.abs(value - exact) / exact > APPROXIMATE_ABOVE + EPS;
	return { value, approximate };
}

/** A servings count as a number: whole, or with a ½ glyph (`2 ½`, `½`). */
export function formatServingsNumber(n: number): string {
	const whole = Math.floor(n + EPS);
	if (n - whole < EPS) return String(whole);
	return whole === 0 ? '½' : `${whole} ½`;
}

/** "6 porcji", "3 porcje", "1 porcja", "~2 ½ porcji" (PROJECT §5.5). */
export function formatServings(n: number, approximate = false): string {
	return `${approximate ? '~' : ''}${formatServingsNumber(n)} ${servingsForms[pluralCategory(n)]}`;
}
