<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { createApi, unwrap } from '$lib/api/client';
	import { defaultFridgeIdOf, liquidUnitsOf } from '$lib/components/fridge/amount';
	import FridgeSwitcher from '$lib/components/fridge/FridgeSwitcher.svelte';
	import NewFridgeDialog from '$lib/components/fridge/NewFridgeDialog.svelte';
	import type { FridgeSummary } from '$lib/components/fridge/types';
	import { Page } from '$lib/components/shell';
	import ShoppingBody from '$lib/components/shopping/ShoppingBody.svelte';
	import { Button, EmptyState, ErrorMessage, Skeleton } from '$lib/components/ui';
	import { t } from '$lib/i18n';

	let { data } = $props();

	const labels = t.shopping;

	let fridges = $state<FridgeSummary[] | null>(null);
	let fridgesError = $state<unknown>(null);
	let creatingFridge = $state(false);

	/** `?fridge=` wins, then the user's default fridge, then the oldest one. */
	let fridgeId = $derived.by(() => {
		if (!fridges || fridges.length === 0) return null;
		const wanted = page.url.searchParams.get('fridge') ?? defaultFridgeIdOf(data);
		return fridges.find((f) => f.id === wanted)?.id ?? fridges[0]?.id ?? null;
	});
	let fridgeName = $derived(fridges?.find((f) => f.id === fridgeId)?.name ?? '');

	async function loadFridges() {
		fridgesError = null;
		try {
			fridges = await unwrap(createApi().GET('/fridges'));
		} catch (e) {
			fridgesError = e;
		}
	}
	onMount(loadFridges);

	function select(id: string) {
		const target = `${resolve('/lista-zakupow')}?fridge=${encodeURIComponent(id)}`;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- built from resolve() above
		void goto(target, { replaceState: true, keepFocus: true, noScroll: true });
	}

	function created(fridge: FridgeSummary) {
		fridges = [...(fridges ?? []), fridge];
		select(fridge.id);
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
		<div class="mb-5">
			<FridgeSwitcher {fridges} value={fridgeId} onSelect={select} onCreated={created} />
		</div>
		{#key fridgeId}
			<ShoppingBody {fridgeId} {fridgeName} liquidUnits={liquidUnitsOf(data)} />
		{/key}
	{/if}
</Page>

{#if creatingFridge}
	<NewFridgeDialog bind:open={creatingFridge} onCreated={created} />
{/if}
