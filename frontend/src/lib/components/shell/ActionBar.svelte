<script lang="ts">
	import type { Snippet } from 'svelte';
	import type { Attachment } from 'svelte/attachments';

	/**
	 * The sticky row of main actions at the bottom of a page (recipe view: cook and deduct
	 * from the fridge; editors: save). Place it last inside `Page`. It sticks just above the tab bar on
	 * phones and to the bottom of the window on desktop. One primary Button at most, placed last.
	 * While mounted it publishes its height as `--rl-actionbar-h`, so toasts rise above it.
	 */
	let { children, label }: { children: Snippet; label?: string } = $props();

	const publishHeight: Attachment<HTMLElement> = (node) => {
		const root = document.documentElement;
		const update = () => root.style.setProperty('--rl-actionbar-h', `${node.offsetHeight}px`);
		update();
		const observer = new ResizeObserver(update);
		observer.observe(node);
		return () => {
			observer.disconnect();
			root.style.removeProperty('--rl-actionbar-h');
		};
	};
</script>

<div
	{@attach publishHeight}
	role="group"
	aria-label={label}
	class="sticky bottom-[calc(var(--rl-tabbar-h)+var(--rl-safe-bottom))] z-20 -mx-4 mt-6 flex items-center gap-2 border-t border-line bg-surface px-4 py-3 md:-mx-6 md:px-6 lg:bottom-0 lg:mx-0 lg:rounded-t-card lg:border-x lg:px-4 [&>*:last-child]:ml-auto"
>
	{@render children()}
</div>
