<script lang="ts">
	import { untrack } from 'svelte';
	import { ApiError } from '$lib/api/client';
	import { baseUnitLabel } from '$lib/components/fridge/amount';
	import { Button, Dialog, ErrorMessage, NumberField, TextField } from '$lib/components/ui';
	import type { ShoppingListItem } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	/**
	 * Changes the amount of a product entry or the text of a free-text entry (the API cannot clear an
	 * amount, so saving needs a value above zero). Mount only while open.
	 */
	type Props = {
		open?: boolean;
		item: ShoppingListItem;
		onSave: (change: { amount?: number; text?: string }) => Promise<void>;
	};

	let { open = $bindable(false), item, onSave }: Props = $props();

	const labels = t.shopping;

	let amount = $state<number | null>(untrack(() => item.amount));
	let text = $state(untrack(() => item.text ?? ''));
	let saving = $state(false);
	let error = $state<unknown>(null);

	let valid = $derived(item.product ? amount !== null && amount > 0 : text.trim().length > 0);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		if (!valid || saving) return;
		saving = true;
		error = null;
		try {
			await onSave(item.product ? { amount: amount! } : { text: text.trim() });
			open = false;
		} catch (e) {
			error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			saving = false;
		}
	}
</script>

<Dialog bind:open title={item.product?.name ?? labels.editTitle}>
	<form id="edit-shopping-form" class="flex flex-col gap-5" onsubmit={submit}>
		<ErrorMessage {error} />
		{#if item.product}
			<NumberField
				label={labels.amountLabel}
				bind:value={amount}
				min={0}
				suffix={baseUnitLabel(item.product)}
			/>
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
