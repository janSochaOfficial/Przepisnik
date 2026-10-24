// The hand-off to the cooking actions of F6 (deduct from the fridge, add missing to the shopping list).
import { createContext } from 'svelte';
import type { Ingredient } from '$lib/domain/types';

/**
 * What the recipe view currently shows: the best (or custom-mix) variant and its effective ingredient
 * list, with every amount already multiplied by the scaling factor (`scaledBaseAmount`, in base units).
 * The properties are getters, so reading them inside `$derived` / templates stays reactive.
 */
export type CookContext = {
	readonly recipeId: string;
	/** A variant uuid or 'base'. */
	readonly variantId: string;
	readonly ingredients: (Ingredient & { scaledBaseAmount: number | null })[];
};

export const [getCookContext, setCookContext] = createContext<CookContext>();
