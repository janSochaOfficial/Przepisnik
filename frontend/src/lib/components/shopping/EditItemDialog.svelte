<script lang="ts">
	import { untrack } from 'svelte';
	import { ApiError } from '$lib/api/client';
	import { Button, Dialog, ErrorMessage, NumberField, TextField } from '$lib/components/ui';
	import UnitSelect from '$lib/components/units/UnitSelect.svelte';
	import type { Quantity, ShoppingListItem } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import { convertDraft, draftOf, draftQuantity } from './quantity';

	/**
	 * Changes the amount (a value and its unit; the clear action removes it) of a product entry or the
	 * text of a free-text entry. Changing the unit converts the value. Mount only while open.
	 */
	type Props = {
		open?: boolean;
		item: ShoppingListItem;
		onSave: (change: {
			quantity?: Quantity;
			clearAmount?: boolean;
			text?: string;
		}) => Promise<void>;
	};

	let { open = $bindable(false), item, onSave }: Props = $props();

	const labels = t.shopping;
	const initial = untrack(() => draftOf(item));

	let value = $state<number | null>(initial.value);
	let unit = $state(initial.unit);
	let customUnit = $state(initial.customUnit);
	let size = initial.size;
	let text = $state(untrack(() => item.text ?? ''));
	let saving = $state(false);
	let error = $state<unknown>(null);

	let quantity = $derived(
		item.product ? draftQuantity({ value, unit, customUnit, size }, item.product) : null
	);
	let valid = $derived(item.product ? quantity !== null : text.trim().length > 0);

	// The unit the value is currently in, so that a change of unit can convert it.
	let shown = { unit: initial.unit, customUnit: initial.customUnit };

	function unitChanged(nextUnit: typeof unit, nextCustom: typeof customUnit) {
		const product = item.product;
		if (product) {
			const converted = convertDraft(
				{ value, unit: shown.unit, customUnit: shown.customUnit, size },
				{ unit: nextUnit, customUnit: nextCustom, size },
				product
			);
			if (converted !== null) value = converted;
		}
		shown = { unit: nextUnit, customUnit: nextCustom };
	}

	async function save(change: Parameters<Props['onSave']>[0]) {
		if (saving) return;
		saving = true;
		error = null;
		try {
			await onSave(change);
			open = false;
		} catch (e) {
			error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			saving = false;
		}
	}

	function submit(event: SubmitEvent) {
		event.preventDefault();
		if (!valid) return;
		void save(item.product ? { quantity: quantity! } : { text: text.trim() });
	}
</script>

<Dialog bind:open title={item.product?.name ?? labels.editTitle}>
	<form id="edit-shopping-form" class="flex flex-col gap-5" onsubmit={submit}>
		<ErrorMessage {error} />
		{#if item.product}
			<div class="grid grid-cols-2 gap-3">
				<NumberField label={labels.amountLabel} bind:value min={0} />
				<UnitSelect
					label={labels.unitLabel}
					product={item.product}
					bind:unit
					bind:customUnit
					onChange={unitChanged}
				/>
			</div>
			{#if item.amount !== null}
				<Button
					variant="ghost"
					class="self-start"
					loading={saving}
					onclick={() => save({ clearAmount: true })}
				>
					{labels.clearAmount}
				</Button>
			{/if}
		{:else}
			<TextField label={labels.textLabel} bind:value={text} maxlength={200} />
		{/if}
	</form>
	{#snippet footer()}
		<Button variant="ghost" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button type="submit" form="edit-shopping-form" loading={saving} disabled={!valid}>
			{t.common.actions.save}
		</Button>
	{/snippet}
</Dialog>
