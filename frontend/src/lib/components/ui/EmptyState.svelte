<script lang="ts">
	import type { Snippet } from 'svelte';
	import Oczko from './Oczko.svelte';
	import type { IconComponent } from './types';

	/**
	 * What to show when a list has nothing yet. The title says what is empty, the text says what to
	 * do, and `action` is the button that does it. Pass `icon` only when it explains more than the
	 * motif (e.g. a search icon for "no results").
	 */
	type Props = {
		title: string;
		description?: string;
		icon?: IconComponent;
		/** Heading level of the title within the page. */
		level?: 2 | 3;
		action?: Snippet;
		class?: string;
	};

	let {
		title,
		description,
		icon: Icon,
		level = 2,
		action,
		class: className = ''
	}: Props = $props();
</script>

<div class={['flex flex-col items-center px-6 py-10 text-center', className]}>
	{#if Icon}
		<span
			class="mb-4 inline-flex size-16 items-center justify-center rounded-full bg-cobalt-wash text-cobalt"
		>
			<Icon size={30} aria-hidden="true" />
		</span>
	{:else}
		<Oczko size={72} class="mb-4" />
	{/if}
	<svelte:element this={`h${level}`} class="font-serif text-xl font-semibold text-ink">
		{title}
	</svelte:element>
	{#if description}
		<p class="mt-2 max-w-[34ch] text-base text-muted">{description}</p>
	{/if}
	{#if action}
		<div class="mt-6">{@render action()}</div>
	{/if}
</div>
