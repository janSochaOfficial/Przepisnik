<script lang="ts">
	import StepList from './StepList.svelte';
	import { toContentBlocks } from './blocks';
	import type { DraftStep, EditorMode } from './draft';

	// Test harness: a StepList plus a read-out of what it would send to the API.
	let { initial, mode = 'recipe' }: { initial: DraftStep[]; mode?: EditorMode } = $props();
	// svelte-ignore state_referenced_locally
	let steps = $state(initial);
	let out = $derived(
		JSON.stringify(steps.map((s) => ({ removed: s.removed, blocks: toContentBlocks(s.blocks) })))
	);
</script>

<StepList bind:steps path="instructionSteps" {mode} />
<output data-testid="out">{out}</output>
