<script lang="ts">
	import BottomSheet from './BottomSheet.svelte';

	// Test harness: a BottomSheet with a trigger and two focusable choices.
	let { onOpenChange }: { onOpenChange?: (open: boolean) => void } = $props();
</script>

<button type="button">before</button>
<BottomSheet title="Zamiennik" {onOpenChange}>
	{#snippet trigger({ props })}
		<button type="button" {...props}>open</button>
	{/snippet}
	<button type="button">first</button>
	<button type="button">second</button>
</BottomSheet>
<button type="button">after</button>
