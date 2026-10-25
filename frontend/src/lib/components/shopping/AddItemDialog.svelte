<script lang="ts">
	import { ApiError } from '$lib/api/client';
	import ProductPicker from '$lib/components/catalog/ProductPicker.svelte';
	import {
		Button,
		Chip,
		Dialog,
		ErrorMessage,
		NumberField,
		TextField,
		toast
	} from '$lib/components/ui';
	import UnitSelect from '$lib/components/units/UnitSelect.svelte';
	import type { CustomUnitRef, ProductRef, Unit } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import type { ShoppingListItemWrite } from './list.svelte';
	import { baseUnitOf, draftQuantity } from './quantity';

	/** Adds a catalog product (with an optional amount) or free text to the list. Mount only while open. */
	type Props = {
		open?: boolean;
		onAdd: (entry: ShoppingListItemWrite) => Promise<void>;
	};

	let { open = $bindable(false), onAdd }: Props = $props();

	const labels = t.shopping;

	let kind = $state<'product' | 'text'>('product');
	let product = $state<ProductRef | null>(null);
	let amount = $state<number | null>(null);
	// The unit the user picked for the chosen product; a new product starts in its base unit.
	let picked = $state<{ productId: string; unit: Unit; customUnit: CustomUnitRef | null } | null>(
		null
	);
	let unit = $derived<Unit>(
		product ? (picked?.productId === product.id ? picked.unit : baseUnitOf(product)) : 'g'
	);
	let customUnit = $derived(product && picked?.productId === product.id ? picked.customUnit : null);
	let text = $state('');
	let saving = $state(false);
	let submitted = $state(false);
	let error = $state<unknown>(null);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		submitted = true;
		if (saving) return;
		let entry: ShoppingListItemWrite;
		if (kind === 'product') {
			if (!product) return;
			const quantity = draftQuantity({ value: amount, unit, customUnit, size: null }, product);
			entry = { productId: product.id, ...(quantity ? { quantity } : {}) };
		} else {
			if (!text.trim()) return;
			entry = { text: text.trim() };
		}
		saving = true;
		error = null;
		try {
			await onAdd(entry);
			open = false;
			toast.success(labels.added);
		} catch (e) {
			error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			saving = false;
		}
	}
</script>

<Dialog bind:open title={labels.addTitle}>
	<form id="add-shopping-form" class="flex flex-col gap-5" onsubmit={submit}>
		<ErrorMessage {error} />
		<div role="group" aria-label={labels.kindLabel} class="flex gap-2">
			<Chip selected={kind === 'product'} onclick={() => (kind = 'product')}>
				{labels.kindProduct}
			</Chip>
			<Chip selected={kind === 'text'} onclick={() => (kind = 'text')}>{labels.kindText}</Chip>
		</div>
		{#if kind === 'product'}
			<ProductPicker
				bind:value={product}
				error={submitted && !product ? labels.productRequired : null}
			/>
			{#if product}
				<div class="grid grid-cols-2 gap-3">
					<NumberField label={labels.amountLabel} bind:value={amount} min={0} optional />
					<UnitSelect
						label={labels.unitLabel}
						{product}
						{unit}
						{customUnit}
						onChange={(next, nextCustom) =>
							(picked = { productId: product?.id ?? '', unit: next, customUnit: nextCustom })}
					/>
				</div>
			{:else}
				<NumberField label={labels.amountLabel} bind:value={amount} min={0} optional disabled />
			{/if}
		{:else}
			<TextField
				label={labels.textLabel}
				bind:value={text}
				placeholder={labels.textPlaceholder}
				maxlength={200}
				error={submitted && !text.trim() ? labels.textRequired : null}
			/>
		{/if}
	</form>
	{#snippet footer()}
		<Button variant="ghost" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button type="submit" form="add-shopping-form" loading={saving}>{labels.add}</Button>
	{/snippet}
</Dialog>
