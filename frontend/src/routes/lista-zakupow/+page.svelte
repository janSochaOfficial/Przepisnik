<script lang="ts">
	import Copy from '@lucide/svelte/icons/copy';
	import Plus from '@lucide/svelte/icons/plus';
	import Share2 from '@lucide/svelte/icons/share-2';
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { createApi, unwrap } from '$lib/api/client';
	import { defaultFridgeIdOf, liquidUnitsOf } from '$lib/components/fridge/amount';
	import { canShare, copyText, shareText } from '$lib/components/fridge/clipboard';
	import FridgeSwitcher from '$lib/components/fridge/FridgeSwitcher.svelte';
	import type { FridgeSummary } from '$lib/components/fridge/types';
	import { ActionBar, Page } from '$lib/components/shell';
	import {
		Button,
		ConfirmDialog,
		EmptyState,
		ErrorMessage,
		Skeleton,
		toast
	} from '$lib/components/ui';
	import AddItemDialog from '$lib/components/shopping/AddItemDialog.svelte';
	import EditItemDialog from '$lib/components/shopping/EditItemDialog.svelte';
	import ItemRow from '$lib/components/shopping/ItemRow.svelte';
	import { ShoppingListState } from '$lib/components/shopping/list.svelte';
	import NewFridgeDialog from '$lib/components/fridge/NewFridgeDialog.svelte';
	import type { ShoppingListItem } from '$lib/domain/types';
	import { shoppingListText } from '$lib/domain/shopping-text';
	import { ApiError } from '$lib/api/client';
	import { t } from '$lib/i18n';

	let { data } = $props();

	const labels = t.shopping;
	const list = new ShoppingListState();

	let fridges = $state<FridgeSummary[] | null>(null);
	let fridgesError = $state<unknown>(null);
	let liquidUnits = $derived(liquidUnitsOf(data));

	let fridgeId = $derived.by(() => {
		if (!fridges || fridges.length === 0) return null;
		const wanted = page.url.searchParams.get('fridge') ?? defaultFridgeIdOf(data);
		return fridges.find((f) => f.id === wanted)?.id ?? fridges[0]?.id ?? null;
	});
	let fridgeName = $derived(fridges?.find((f) => f.id === fridgeId)?.name ?? '');

	let adding = $state(false);
	let editing = $state<ShoppingListItem | null>(null);
	let editOpen = $state(false);
	let confirmRemove = $state(false);
	let creatingFridge = $state(false);
	const shareable = canShare();

	async function loadFridges() {
		fridgesError = null;
		try {
			fridges = await unwrap(createApi().GET('/fridges'));
		} catch (e) {
			fridgesError = e;
		}
	}
	onMount(loadFridges);

	$effect(() => {
		const id = fridgeId;
		if (id) void list.load(id);
	});

	function select(id: string) {
		const target = `${resolve('/lista-zakupow')}?fridge=${encodeURIComponent(id)}`;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- built from resolve() above
		void goto(target, { replaceState: true, keepFocus: true, noScroll: true });
	}

	function fail(e: unknown) {
		toast.error(
			e instanceof ApiError && e.code !== 'server.error' && e.status !== 409
				? (t.errors[e.code as keyof typeof t.errors] ?? labels.saveFailed)
				: e instanceof ApiError && e.status === 409
					? labels.conflict
					: labels.saveFailed
		);
	}

	async function toggle(item: ShoppingListItem) {
		try {
			await list.toggle(item.id);
		} catch (e) {
			fail(e);
		}
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

<Page title={labels.title}>
	{#if fridgesError}
		<ErrorMessage error={fridgesError} variant="block" retry={loadFridges} />
	{:else if !fridges}
		<div class="flex flex-col gap-4" aria-busy="true">
			<Skeleton class="h-12 w-full" />
			<Skeleton shape="block" class="h-48 w-full" />
		</div>
	{:else if !fridgeId}
		<EmptyState title={labels.noFridgeTitle} description={labels.noFridgeText}>
			{#snippet action()}
				<Button icon={Plus} onclick={() => (creatingFridge = true)}>
					{t.fridge.switcher.newFridge}
				</Button>
			{/snippet}
		</EmptyState>
	{:else}
		<div class="flex flex-col gap-5">
			<FridgeSwitcher
				{fridges}
				value={fridgeId}
				onSelect={select}
				onCreated={(f) => {
					fridges = [...(fridges ?? []), f];
					select(f.id);
				}}
			/>

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
								<ItemRow
									{item}
									{liquidUnits}
									onToggle={toggle}
									onEdit={(i) => {
										editing = i;
										editOpen = true;
									}}
								/>
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
								<ItemRow
									{item}
									{liquidUnits}
									onToggle={toggle}
									onEdit={(i) => {
										editing = i;
										editOpen = true;
									}}
								/>
							{/each}
						</ul>
						<div class="mt-3 flex flex-wrap gap-2">
							<Button variant="secondary" onclick={() => complete(true)}>{labels.moveBought}</Button
							>
							<Button variant="ghost" onclick={() => (confirmRemove = true)}>
								{labels.removeBought}
							</Button>
						</div>
					</section>
				{/if}
			{/if}
		</div>

		<ActionBar>
			<Button icon={Plus} onclick={() => (adding = true)}>{labels.add}</Button>
		</ActionBar>
	{/if}
</Page>

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
{#if creatingFridge}
	<NewFridgeDialog
		bind:open={creatingFridge}
		onCreated={(f) => {
			fridges = [...(fridges ?? []), f];
			select(f.id);
		}}
	/>
{/if}
<ConfirmDialog
	bind:open={confirmRemove}
	title={labels.confirmRemoveTitle}
	description={labels.confirmRemoveText}
	confirmLabel={labels.removeBought}
	onConfirm={() => complete(false)}
/>
