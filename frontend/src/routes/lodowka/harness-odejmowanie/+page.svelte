<script lang="ts">
	import { page } from '$app/state';
	import { addMissingToShoppingList } from '$lib/components/deduction/addMissing';
	import DeductionDialog from '$lib/components/deduction/DeductionDialog.svelte';
	import { defaultFridgeIdOf, liquidUnitsOf } from '$lib/components/fridge/amount';
	import { ActionBar, Page } from '$lib/components/shell';
	import { Button } from '$lib/components/ui';
	import type { Ingredient, ProductRef } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	let { data } = $props();

	const product = (id: string, name: string, over: Partial<ProductRef> = {}): ProductRef => ({
		id,
		name,
		category: 'other',
		measureKind: 'volume',
		displayClass: 'liquid',
		hasSizes: false,
		allowHalf: false,
		gramsPerMl: null,
		gramsPerPiece: null,
		defaultPackageAmount: null,
		pieceForms: null,
		isVerified: true,
		...over
	});
	const ingredient = (id: string, p: ProductRef, baseAmount: number | null): Ingredient => ({
		id,
		product: p,
		quantity: baseAmount === null ? null : { value: baseAmount, unit: 'ml' },
		baseAmount,
		note: null,
		optional: false
	});

	// Sample recipe: smietanka (short in the fixtures), maka (missing), jajka (enough), sol (do smaku).
	const base: Ingredient[] = [
		ingredient('i1', product('00000000-0000-7000-8000-00000000c001', 'smietanka'), 330),
		ingredient(
			'i2',
			product('00000000-0000-7000-8000-00000000c002', 'maka', {
				measureKind: 'mass',
				displayClass: 'weight'
			}),
			200
		),
		ingredient(
			'i3',
			product('00000000-0000-7000-8000-00000000c003', 'jajka', {
				measureKind: 'count',
				displayClass: 'piece'
			}),
			2
		),
		ingredient('i4', product('00000000-0000-7000-8000-00000000c004', 'sol'), null)
	];

	// `?optional=1` adds a missing optional ingredient (rodzynki) for the add-missing question.
	const ingredients: Ingredient[] = page.url.searchParams.has('optional')
		? [
				...base,
				{
					...ingredient(
						'i5',
						product('00000000-0000-7000-8000-00000000c005', 'rodzynki', {
							measureKind: 'mass',
							displayClass: 'weight'
						}),
						50
					),
					optional: true
				}
			]
		: base;

	let open = $state(false);
</script>

<Page title={t.fridge.deduction.title}>
	<p class="text-base text-muted">{ingredients.map((i) => i.product.name).join(', ')}</p>
	<ActionBar>
		<Button
			variant="secondary"
			onclick={() =>
				addMissingToShoppingList({
					ingredients,
					factor: 1,
					fridgeId: defaultFridgeIdOf(data)
				})}
		>
			{t.fridge.deduction.addMissing}
		</Button>
		<Button onclick={() => (open = true)}>{t.fridge.deduction.open}</Button>
	</ActionBar>
</Page>

<DeductionDialog
	bind:open
	{ingredients}
	recipeId="00000000-0000-7000-8000-0000000e0001"
	defaultFridgeId={defaultFridgeIdOf(data)}
	liquidUnits={liquidUnitsOf(data)}
/>
