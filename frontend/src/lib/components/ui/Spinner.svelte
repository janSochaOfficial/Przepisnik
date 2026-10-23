<script lang="ts">
	import LoaderCircle from '@lucide/svelte/icons/loader-circle';

	/** Decorative spinner; the owner sets `aria-busy` and keeps an accessible label. */
	let { size = 20, class: className = '' }: { size?: number; class?: string } = $props();
</script>

<LoaderCircle {size} aria-hidden="true" class={['animate-rl-spin shrink-0', className]} />
