<script lang="ts">
	import ArrowLeft from '@lucide/svelte/icons/arrow-left';
	import type { Snippet } from 'svelte';
	import { t } from '$lib/i18n';
	import IconButton from '$lib/components/ui/IconButton.svelte';

	/**
	 * The top of a page: optional back button, the page's only `<h1>`, and an `actions` slot for up to
	 * two IconButtons (more go into a `t.common.actions.more` menu). Sticks to the top on phones so back and actions
	 * stay in reach; scrolls away on desktop where the rail gives orientation.
	 */
	type Props = {
		title: string;
		/** Back target: a resolved href, or a function (e.g. `history.back()`). */
		back?: string | (() => void);
		backLabel?: string;
		/** One short muted line under the title (author, fridge name …). */
		subtitle?: string;
		actions?: Snippet;
	};

	let { title, back, backLabel = t.common.actions.back, subtitle, actions }: Props = $props();
</script>

<header
	class="sticky top-0 z-20 -mx-4 flex min-h-(--rl-header-h) items-center gap-1 bg-paper/95 px-2 pt-(--rl-safe-top) backdrop-blur-sm md:-mx-6 md:px-4 lg:static lg:mx-0 lg:bg-transparent lg:px-0 lg:pt-8 lg:pb-2 lg:backdrop-blur-none"
>
	{#if back}
		{#if typeof back === 'string'}
			<IconButton icon={ArrowLeft} label={backLabel} href={back} class="lg:-ml-3" />
		{:else}
			<IconButton icon={ArrowLeft} label={backLabel} onclick={back} class="lg:-ml-3" />
		{/if}
	{/if}
	<div class={['min-w-0 flex-1 py-2', !back && 'pl-2 lg:pl-0']}>
		<h1 class="truncate text-xl lg:text-3xl lg:whitespace-normal">{title}</h1>
		{#if subtitle}
			<p class="truncate text-sm text-muted">{subtitle}</p>
		{/if}
	</div>
	{#if actions}
		<div class="flex shrink-0 items-center gap-1">{@render actions()}</div>
	{/if}
</header>
