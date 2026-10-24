<script lang="ts">
	import { page } from '$app/state';
	import { liquidUnitsOf } from '$lib/components/fridge/amount';
	import FridgeScreen from '$lib/components/fridge/FridgeScreen.svelte';

	let { data } = $props();
</script>

{#key page.params.id}
	<FridgeScreen
		fridgeId={page.params.id ?? ''}
		liquidUnits={liquidUnitsOf(data)}
		meId={data.me?.id ?? ''}
	/>
{/key}
