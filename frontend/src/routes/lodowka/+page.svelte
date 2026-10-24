<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import NewFridgeDialog from '$lib/components/fridge/NewFridgeDialog.svelte';
	import { Page } from '$lib/components/shell';
	import { Button, EmptyState } from '$lib/components/ui';
	import { t } from '$lib/i18n';

	let creating = $state(false);
</script>

<Page title={t.fridge.title}>
	<EmptyState title={t.fridge.empty.noFridgeTitle} description={t.fridge.empty.noFridgeText}>
		{#snippet action()}
			<Button icon={Plus} onclick={() => (creating = true)}>{t.fridge.switcher.newFridge}</Button>
		{/snippet}
	</EmptyState>
</Page>

{#if creating}
	<NewFridgeDialog
		bind:open={creating}
		onCreated={(f) => goto(resolve('/lodowka/[id]', { id: f.id }))}
	/>
{/if}
