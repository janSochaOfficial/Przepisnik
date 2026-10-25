<script lang="ts">
	import type { Snippet } from 'svelte';
	import PageHeader from './PageHeader.svelte';

	/**
	 * The standard page: horizontal gutters, a width, a `PageHeader` and the content. Every screen
	 * of F3–F6 starts with `<Page title=…>`.
	 * - `reading` (default, 45 rem): forms, the recipe view, lists of rows;
	 * - `wide` (72 rem): card grids (recipe catalog).
	 */
	type Props = {
		title: string;
		back?: string | (() => void);
		backLabel?: string;
		subtitle?: string;
		width?: 'reading' | 'wide';
		actions?: Snippet;
		titleAside?: Snippet;
		children: Snippet;
	};

	let {
		title,
		back,
		backLabel,
		subtitle,
		width = 'reading',
		actions,
		titleAside,
		children
	}: Props = $props();
</script>

<div
	class={[
		'mx-auto w-full px-4 pb-8 md:px-6 lg:px-10',
		width === 'reading' ? 'max-w-[calc(45rem+5rem)]' : 'max-w-[calc(72rem+5rem)]'
	]}
>
	<PageHeader {title} {back} {backLabel} {subtitle} {actions} {titleAside} />
	<div class="pt-2 lg:pt-4">
		{@render children()}
	</div>
</div>
