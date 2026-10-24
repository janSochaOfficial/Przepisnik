<script lang="ts">
	import type { DeductionResult } from '$lib/components/fridge/types';
	import type { Ingredient, LiquidUnits } from '$lib/domain/types';
	import DeductionPanel from './DeductionPanel.svelte';

	/**
	 * "Odejmij z lodówki" (PROJECT §9), to be hosted by the recipe view with the **resolved**
	 * ingredients and the scaling `factor`:
	 *
	 * ```svelte
	 * <DeductionDialog bind:open ingredients={resolved.ingredients} {factor}
	 *   recipeId={recipe.id} {variantId} liquidUnits={me.settings.liquidUnits}
	 *   defaultFridgeId={me.settings.defaultFridgeId} />
	 * ```
	 * It loads the fridges and the chosen fridge's items itself every time it opens, and deducts
	 * with an undo toast.
	 */
	type Props = {
		open?: boolean;
		/** The currently resolved ingredients (variant picks applied). "Do smaku" rows are ignored. */
		ingredients: Ingredient[];
		/** The scaling factor of the recipe view (1 = as written). */
		factor?: number;
		recipeId?: string | null;
		variantId?: string | null;
		defaultFridgeId?: string | null;
		liquidUnits: LiquidUnits;
		onDeducted?: (result: DeductionResult) => void;
	};

	let { open = $bindable(false), ...rest }: Props = $props();
</script>

{#if open}
	<DeductionPanel bind:open {...rest} />
{/if}
