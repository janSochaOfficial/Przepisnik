<script lang="ts">
	/**
	 * The "oczko" (peacock eye) of Boleslawiec stoneware: the one decorative motif of the app
	 * (DESIGN.md "Signature"). Used only in the rail brand mark and in EmptyState.
	 * Always decorative (aria-hidden).
	 */
	type Props = {
		size?: number;
		/** `full` = dot, ring, dotted ring and outer ring; `small` = dot and dotted ring. */
		variant?: 'full' | 'small';
		class?: string;
	};

	let { size = 48, variant = 'full', class: className = '' }: Props = $props();

	const DOTS = 12;
	let dotRing = $derived(variant === 'full' ? 15 : 13);
	let dots = $derived(
		Array.from({ length: DOTS }, (_, i) => {
			const angle = (i / DOTS) * Math.PI * 2;
			return {
				id: i,
				cx: 24 + Math.cos(angle) * dotRing,
				cy: 24 + Math.sin(angle) * dotRing
			};
		})
	);
</script>

<svg
	width={size}
	height={size}
	viewBox="0 0 48 48"
	aria-hidden="true"
	focusable="false"
	class={['shrink-0', className]}
>
	{#if variant === 'full'}
		<circle cx="24" cy="24" r="22" fill="none" stroke="var(--rl-cobalt)" stroke-width="1.5" />
		<circle cx="24" cy="24" r="9" fill="none" stroke="var(--rl-cobalt)" stroke-width="3" />
	{/if}
	<circle cx="24" cy="24" r={variant === 'full' ? 4.5 : 7} fill="var(--rl-dill)" />
	{#each dots as dot (dot.id)}
		<circle cx={dot.cx} cy={dot.cy} r={variant === 'full' ? 2 : 3} fill="var(--rl-cobalt)" />
	{/each}
</svg>
