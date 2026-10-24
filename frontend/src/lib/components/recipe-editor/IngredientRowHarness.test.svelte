<script lang="ts">
	import IngredientRow from './IngredientRow.svelte';
	import { emptyIngredient, type DraftIngredient } from './draft';
	import type { ProductRef } from '$lib/domain/types';

	// Test harness: one ingredient row holding `product`.
	let {
		product,
		unit,
		customUnit
	}: {
		product: ProductRef;
		unit?: DraftIngredient['unit'];
		customUnit?: DraftIngredient['customUnit'];
	} = $props();
	// svelte-ignore state_referenced_locally
	let ingredient = $state({
		...emptyIngredient(),
		product,
		unit: unit ?? null,
		customUnit: customUnit ?? null
	});
</script>

<IngredientRow bind:ingredient />
