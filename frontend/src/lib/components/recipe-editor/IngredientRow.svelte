<script lang="ts">
	import { NumberField, Select, Switch, TextField } from '$lib/components/ui';
	import ProductPicker from '$lib/components/catalog/ProductPicker.svelte';
	import type { PieceSize, ProductRef, Unit } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import { compatibleUnits, defaultUnit, type DraftIngredient } from './draft';

	/**
	 * The fields of one ingredient: product (with inline "Dodaj produkt"), amount, a unit select that
	 * lists only the units compatible with the product (F1 `toBaseAmount` ≠ null), the size for
	 * products with sizes, "do smaku", "opcjonalny" and a note.
	 */
	type Props = {
		ingredient: DraftIngredient;
		errors?: { product?: string; quantity?: string; note?: string };
	};

	let { ingredient = $bindable(), errors = {} }: Props = $props();

	const labels = t.recipes.ingredients;

	let units = $derived(compatibleUnits(ingredient.product));
	let unitItems = $derived(units.map((u) => ({ value: u, label: labels.unitLabels[u] })));
	const sizeItems = (['small', 'medium', 'large'] as const).map((s) => ({
		value: s,
		label: labels.sizeLabels[s]
	}));
	let showSize = $derived(ingredient.unit === 'piece' && !!ingredient.product?.hasSizes);

	function productChanged(product: ProductRef | null) {
		if (!product) return;
		if (ingredient.unit === null || !compatibleUnits(product).includes(ingredient.unit)) {
			ingredient.unit = defaultUnit(product);
		}
		if (!product.hasSizes) ingredient.size = null;
	}
</script>

<div class="flex flex-col gap-3">
	<ProductPicker
		bind:value={ingredient.product}
		error={errors.product ?? null}
		onValueChange={productChanged}
	/>
	{#if !ingredient.toTaste}
		<div class="grid grid-cols-[1fr_1fr] gap-3">
			<NumberField
				label={labels.quantity}
				bind:value={ingredient.value}
				min={0}
				error={errors.quantity ?? null}
			/>
			<Select
				label={labels.unit}
				items={unitItems}
				value={ingredient.unit ?? ''}
				onValueChange={(v) => (ingredient.unit = v as Unit)}
			/>
		</div>
		{#if showSize}
			<Select
				label={labels.size}
				items={sizeItems}
				value={ingredient.size ?? 'medium'}
				onValueChange={(v) => (ingredient.size = v as PieceSize)}
			/>
		{/if}
	{/if}
	<Switch
		label={labels.toTaste}
		description={labels.toTasteHint}
		bind:checked={ingredient.toTaste}
	/>
	<Switch label={labels.optional} bind:checked={ingredient.optional} />
	<TextField
		label={labels.note}
		optional
		maxlength={300}
		placeholder={labels.notePlaceholder}
		bind:value={ingredient.note}
		error={errors.note ?? null}
	/>
</div>
