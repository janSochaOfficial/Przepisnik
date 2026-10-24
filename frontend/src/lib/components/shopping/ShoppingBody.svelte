<script lang="ts">
	import Copy from '@lucide/svelte/icons/copy';
	import Plus from '@lucide/svelte/icons/plus';
	import Share2 from '@lucide/svelte/icons/share-2';
	import { onMount } from 'svelte';
	import { ApiError } from '$lib/api/client';
	import { canShare, copyText, shareText } from '$lib/components/fridge/clipboard';
	import { ActionBar } from '$lib/components/shell';
	import {
		Button,
		ConfirmDialog,
		EmptyState,
		ErrorMessage,
		Skeleton,
		toast
	} from '$lib/components/ui';
	import { shoppingListText } from '$lib/domain/shopping-text';
	import type { LiquidUnits, ShoppingListItem } from '$lib/domain/types';
	import { errorMessage, t } from '$lib/i18n';
	import AddItemDialog from './AddItemDialog.svelte';
	import EditItemDialog from './EditItemDialog.svelte';
	import ItemRow from './ItemRow.svelte';
	import { ShoppingListState } from './list.svelte';

	/**
	 * The list of one fridge with its actions. The route renders it inside `{#key fridgeId}`, so it loads
	 * once per fridge. Place it inside `Page` (it ends with the `ActionBar`).
	 */
	type Props = {
		fridgeId: string;
		fridgeName: string;
		liquidUnits: LiquidUnits;
	};

	let { fridgeId, fridgeName, liquidUnits }: Props = $props();

	const labels = t.shopping;
	const list = new ShoppingListState();
	const shareable = canShare();

	let adding = $state(false);
	let editing = $state<ShoppingListItem | null>(null);
	let editOpen = $state(false);
	let confirmRemove = $state(false);

	onMount(() => list.load(fridgeId));

	function fail(e: unknown) {
		if (e instanceof ApiError && e.status === 409) toast.error(labels.conflict);
		else if (e instanceof ApiError) toast.error(errorMessage(e.code));
		else toast.error(labels.saveFailed);
	}

	async function toggle(item: ShoppingListItem) {
		try {
			await list.toggle(item.id);
		} catch (e) {
			fail(e);
		}
	}

	function edit(item: ShoppingListItem) {
		editing = item;
		editOpen = true;
	}

	async function complete(moveToFridge: boolean) {
		try {
			const result = await list.complete(moveToFridge);
			toast.success(moveToFridge ? labels.moved(result.moved) : labels.removed(result.removed));
			// Checked product entries without an amount or a default package stay on the list (B5).
			if (moveToFridge && list.checked.length > 0) {
				toast.show(labels.leftOver(list.checked.length), { duration: 10000 });
			}
		} catch (e) {
			fail(e);
		}
	}

	let text = $derived(shoppingListText(fridgeName, list.items, liquidUnits));

	async function copy() {
		if (await copyText(text)) toast.success(labels.copied);
		else toast.error(labels.copyFailed);
	}

	async function share() {
		await shareText({ title: labels.shareTitle(fridgeName), text });
	}
</script>

{#if list.error}
	<ErrorMessage error={list.error} variant="block" retry={() => list.load(fridgeId)} />
{:else if list.loading}
	<div class="flex flex-col gap-3" aria-busy="true">
		<Skeleton class="h-14 w-full" />
		<Skeleton class="h-14 w-full" />
		<Skeleton class="h-14 w-full" />
	</div>
{:else if list.items.length === 0}
	<EmptyState title={labels.emptyTitle} description={labels.emptyText} />
{:else}
	<div class="flex flex-col gap-5">
		<div class="flex flex-wrap gap-2">
			{#if shareable}
				<Button variant="secondary" icon={Share2} onclick={share}>{labels.share}</Button>
			{/if}
			<Button variant="secondary" icon={Copy} onclick={copy}>{labels.copy}</Button>
		</div>

		{#if list.unchecked.length > 0}
			<section aria-labelledby="to-buy-heading">
				<h2 id="to-buy-heading" class="mb-2 font-sans text-base font-semibold text-muted">
					{labels.toBuy}
				</h2>
				<ul class="overflow-hidden rounded-card border border-line bg-surface">
					{#each list.unchecked as item (item.id)}
						<ItemRow {item} {liquidUnits} onToggle={toggle} onEdit={edit} />
					{/each}
				</ul>
			</section>
		{/if}

		{#if list.checked.length > 0}
			<section aria-labelledby="bought-heading">
				<h2 id="bought-heading" class="mb-2 font-sans text-base font-semibold text-muted">
					{labels.bought}
				</h2>
				<ul class="overflow-hidden rounded-card border border-line bg-surface">
					{#each list.checked as item (item.id)}
						<ItemRow {item} {liquidUnits} onToggle={toggle} onEdit={edit} />
					{/each}
				</ul>
				<div class="mt-3 flex flex-wrap gap-2">
					<Button variant="secondary" onclick={() => complete(true)}>{labels.moveBought}</Button>
					<Button variant="ghost" onclick={() => (confirmRemove = true)}>
						{labels.removeBought}
					</Button>
				</div>
			</section>
		{/if}
	</div>
{/if}

<ActionBar>
	<Button icon={Plus} onclick={() => (adding = true)}>{labels.add}</Button>
</ActionBar>

{#if adding}
	<AddItemDialog bind:open={adding} onAdd={(entry) => list.add([entry])} />
{/if}
{#if editing && editOpen}
	<EditItemDialog
		bind:open={editOpen}
		item={editing}
		onSave={(change) => list.patch(editing!.id, change)}
	/>
{/if}
<ConfirmDialog
	bind:open={confirmRemove}
	title={labels.confirmRemoveTitle}
	description={labels.confirmRemoveText}
	confirmLabel={labels.removeBought}
	onConfirm={() => complete(false)}
/>
