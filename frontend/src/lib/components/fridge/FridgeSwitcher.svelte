<script lang="ts">
	import { Select } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import NewFridgeDialog from './NewFridgeDialog.svelte';
	import type { FridgeSummary } from './types';

	/**
	 * Picks one of the user's fridges, with "Nowa lodówka" as the last choice (it opens a small name
	 * dialog and reports the created fridge through `onCreated`).
	 */
	type Props = {
		fridges: FridgeSummary[];
		/** The selected fridge id. */
		value: string | null;
		onSelect: (id: string) => void;
		onCreated: (fridge: FridgeSummary) => void;
		class?: string;
	};

	let { fridges, value, onSelect, onCreated, class: className = '' }: Props = $props();

	const NEW = '__new__';
	const labels = t.fridge.switcher;

	let items = $derived([
		...fridges.map((f) => ({ value: f.id, label: f.name })),
		{ value: NEW, label: labels.newFridge }
	]);
	let selected = $derived(value ?? '');

	let creating = $state(false);

	function handleChange(next: string) {
		if (next === NEW) {
			selected = value ?? '';
			creating = true;
		} else if (next && next !== value) {
			onSelect(next);
		}
	}
</script>

<Select
	label={labels.label}
	{items}
	bind:value={selected}
	onValueChange={handleChange}
	class={className}
/>

{#if creating}
	<NewFridgeDialog bind:open={creating} {onCreated} />
{/if}
