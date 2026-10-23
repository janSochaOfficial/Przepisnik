<script lang="ts">
	import { Tabs } from 'bits-ui';
	import type { Snippet } from 'svelte';
	import type { TabItem } from './types';

	/**
	 * Switches between views of one page (e.g. Wszystkie / Moje przepisy / Moje warianty). The tab
	 * strip scrolls sideways on narrow screens. `panel` renders the content of the selected tab only.
	 * Arrow keys move between tabs (bits-ui).
	 */
	type Props = {
		items: TabItem[];
		value?: string;
		/** Accessible name of the tab list. */
		label: string;
		onValueChange?: (value: string) => void;
		panel?: Snippet<[string]>;
		class?: string;
	};

	let {
		items,
		value = $bindable(items[0]?.value ?? ''),
		label,
		onValueChange,
		panel,
		class: className = ''
	}: Props = $props();
</script>

<Tabs.Root bind:value {onValueChange} class={className}>
	<Tabs.List
		aria-label={label}
		class="-mx-4 flex gap-1 overflow-x-auto border-b border-line px-4 [scrollbar-width:none]"
	>
		{#each items as item (item.value)}
			<Tabs.Trigger
				value={item.value}
				class="relative -mb-px inline-flex min-h-11 shrink-0 items-center gap-2 border-b-3 border-transparent px-3 text-base font-semibold whitespace-nowrap text-muted hover:text-ink data-[state=active]:border-cobalt data-[state=active]:text-ink"
			>
				{item.label}
				{#if item.count !== undefined}
					<span class="tabular rounded-full bg-sunken px-2 text-sm text-muted">{item.count}</span>
				{/if}
			</Tabs.Trigger>
		{/each}
	</Tabs.List>
	{#if panel}
		{#each items as item (item.value)}
			<Tabs.Content value={item.value} class="pt-4">
				{#if item.value === value}
					{@render panel(item.value)}
				{/if}
			</Tabs.Content>
		{/each}
	{/if}
</Tabs.Root>
