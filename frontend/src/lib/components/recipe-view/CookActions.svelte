<script lang="ts">
	import { page } from '$app/state';
	import { addMissingToShoppingList } from '$lib/components/deduction/addMissing';
	import DeductionDialog from '$lib/components/deduction/DeductionDialog.svelte';
	import { defaultFridgeIdOf, liquidUnitsOf } from '$lib/components/fridge/amount';
	import { ActionBar } from '$lib/components/shell';
	import { Button } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { getCookContext } from './cook-context';

	// The sticky bottom bar of the recipe view: the deduction dialog and "add missing to the list"
	// (F6). Both read the current, already scaled ingredients from the cook context.
	const cook = getCookContext();

	let open = $state(false);
	let adding = $state(false);

	// The context amounts are already multiplied by the scaling factor, so the factor here is 1.
	let ingredients = $derived(
		cook.ingredients.map((i) => ({ ...i, baseAmount: i.scaledBaseAmount }))
	);
	let variantId = $derived(cook.variantId === 'base' ? null : cook.variantId);

	async function addMissing() {
		adding = true;
		try {
			await addMissingToShoppingList({
				ingredients,
				factor: 1,
				fridgeId: defaultFridgeIdOf(page.data)
			});
		} finally {
			adding = false;
		}
	}
</script>

<ActionBar label={t.variants.actions.group}>
	<Button variant="secondary" loading={adding} onclick={addMissing}>
		{t.variants.actions.addMissing}
	</Button>
	<Button onclick={() => (open = true)}>{t.variants.actions.deduct}</Button>
</ActionBar>

<DeductionDialog
	bind:open
	{ingredients}
	factor={1}
	recipeId={cook.recipeId}
	{variantId}
	defaultFridgeId={defaultFridgeIdOf(page.data)}
	liquidUnits={liquidUnitsOf(page.data)}
/>
