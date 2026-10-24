<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import SearchX from '@lucide/svelte/icons/search-x';
	import Settings from '@lucide/svelte/icons/settings';
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { createApi, unwrap } from '$lib/api/client';
	import AddItemDialog from './AddItemDialog.svelte';
	import FridgeSettingsDialog from './FridgeSettingsDialog.svelte';
	import FridgeSwitcher from './FridgeSwitcher.svelte';
	import { filterItems } from './group';
	import ItemEditDialog from './ItemEditDialog.svelte';
	import ItemList from './ItemList.svelte';
	import type { FridgeDetail, FridgeSummary } from './types';
	import { ActionBar, Page } from '$lib/components/shell';
	import {
		Button,
		EmptyState,
		ErrorMessage,
		IconButton,
		Skeleton,
		TextField,
		toast
	} from '$lib/components/ui';
	import type { FridgeItem, LiquidUnits } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	/**
	 * One fridge, loaded when it is mounted; the route renders it inside `{#key fridgeId}`, so
	 * switching fridges starts from a clean state.
	 */
	type Props = {
		fridgeId: string;
		liquidUnits: LiquidUnits;
		meId: string;
	};

	let { fridgeId, liquidUnits, meId }: Props = $props();

	const labels = t.fridge;

	let fridges = $state<FridgeSummary[]>([]);
	let detail = $state<FridgeDetail | null>(null);
	let items = $state<FridgeItem[]>([]);
	let loading = $state(true);
	let error = $state<unknown>(null);
	let query = $state('');

	let adding = $state(false);
	let editing = $state<FridgeItem | null>(null);
	let editOpen = $state(false);
	let settingsOpen = $state(false);

	let visible = $derived(filterItems(items, query));

	let requestId = 0;
	async function load(id: string) {
		const current = ++requestId;
		loading = true;
		error = null;
		const api = createApi();
		try {
			const [list, fridge, rows] = await Promise.all([
				unwrap(api.GET('/fridges')),
				unwrap(api.GET('/fridges/{id}', { params: { path: { id } } })),
				unwrap(api.GET('/fridges/{id}/items', { params: { path: { id } } }))
			]);
			if (current !== requestId) return;
			fridges = list;
			detail = fridge;
			items = rows;
		} catch (e) {
			if (current === requestId) error = e;
		} finally {
			if (current === requestId) loading = false;
		}
	}

	onMount(() => load(fridgeId));

	function open(id: string) {
		void goto(resolve('/lodowka/[id]', { id }));
	}

	function added(item: FridgeItem) {
		items = [...items, item];
		void refreshSummary();
	}

	function saved(item: FridgeItem) {
		items = items.map((i) => (i.id === item.id ? item : i));
	}

	async function restore(item: FridgeItem) {
		try {
			const again = await unwrap(
				createApi().POST('/fridges/{id}/items', {
					params: { path: { id: fridgeId } },
					body: {
						productId: item.product.id,
						packageAmount: item.packageAmount,
						remainingPercent: Math.max(item.remainingPercent, 1),
						...(item.note ? { note: item.note } : {})
					}
				})
			);
			items = [...items, again];
		} catch {
			toast.error(labels.edit.restoreFailed);
		}
	}

	function removed(item: FridgeItem) {
		items = items.filter((i) => i.id !== item.id);
		toast.show(labels.edit.deleted, {
			action: { label: t.common.actions.undo, onAction: () => restore(item) }
		});
	}

	async function refreshSummary() {
		try {
			fridges = await unwrap(createApi().GET('/fridges'));
		} catch {
			// The switcher only shows names; a stale count is harmless.
		}
	}

	function renamed(fridge: FridgeSummary) {
		fridges = fridges.map((f) => (f.id === fridge.id ? fridge : f));
		if (detail) detail = { ...detail, name: fridge.name };
	}

	async function gone() {
		const rest = fridges.filter((f) => f.id !== fridgeId);
		if (rest[0]) await goto(resolve('/lodowka/[id]', { id: rest[0].id }));
		else await goto(resolve('/lodowka'));
	}
</script>

<Page title={labels.title}>
	{#snippet actions()}
		<IconButton
			icon={Settings}
			label={labels.settings.button}
			disabled={!detail}
			onclick={() => (settingsOpen = true)}
		/>
	{/snippet}

	{#if error}
		<ErrorMessage {error} variant="block" retry={() => load(fridgeId)} />
	{:else if loading && !detail}
		<div class="flex flex-col gap-4" aria-busy="true">
			<Skeleton class="h-12 w-full" />
			<Skeleton shape="block" class="h-48 w-full" />
		</div>
	{:else}
		<div class="flex flex-col gap-5">
			<FridgeSwitcher {fridges} value={fridgeId} onSelect={open} onCreated={(f) => open(f.id)} />
			{#if items.length > 0}
				<TextField
					label={labels.search}
					type="search"
					bind:value={query}
					hideLabel
					placeholder={labels.search}
				/>
			{/if}

			{#if items.length === 0}
				<EmptyState title={labels.empty.title} description={labels.empty.text} />
			{:else if visible.length === 0}
				<EmptyState
					title={labels.empty.noResultsTitle}
					description={labels.empty.noResultsText}
					icon={SearchX}
				/>
			{:else}
				<ItemList
					items={visible}
					{liquidUnits}
					onEdit={(item) => {
						editing = item;
						editOpen = true;
					}}
				/>
			{/if}
		</div>

		<ActionBar>
			<Button icon={Plus} onclick={() => (adding = true)}>{labels.add.button}</Button>
		</ActionBar>
	{/if}
</Page>

{#if adding}
	<AddItemDialog bind:open={adding} {fridgeId} onAdded={added} />
{/if}
{#if editing && editOpen}
	<ItemEditDialog
		bind:open={editOpen}
		{fridgeId}
		item={editing}
		{liquidUnits}
		onSaved={saved}
		onRemoved={removed}
		onConflict={() => load(fridgeId)}
	/>
{/if}
{#if settingsOpen && detail}
	<FridgeSettingsDialog
		bind:open={settingsOpen}
		fridge={detail}
		{meId}
		onRenamed={renamed}
		onMembersChanged={() => load(fridgeId)}
		onGone={gone}
	/>
{/if}
