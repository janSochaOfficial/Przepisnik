<script lang="ts">
	import type { HTMLTextareaAttributes } from 'svelte/elements';
	import Field from './Field.svelte';
	import { controlBox } from './variants';

	/** Multi-line text. It grows with its content (CSS `field-sizing`), starting at `rows`. */
	type Props = Omit<HTMLTextareaAttributes, 'class' | 'value'> & {
		label: string;
		value?: string;
		hint?: string;
		error?: string | null;
		optional?: boolean;
		hideLabel?: boolean;
		rows?: number;
		class?: string;
	};

	let {
		label,
		value = $bindable(''),
		hint,
		error = null,
		optional = false,
		hideLabel = false,
		rows = 3,
		class: className = '',
		id: idProp,
		...rest
	}: Props = $props();

	const uid = $props.id();
	let id = $derived(idProp ?? `ta-${uid}`);
</script>

<Field {label} {hint} {error} {optional} {hideLabel} {id} class={className}>
	{#snippet control({ id, describedBy, invalid })}
		<textarea
			{...rest}
			{id}
			{rows}
			bind:value
			required={!optional || undefined}
			aria-describedby={describedBy}
			aria-invalid={invalid || undefined}
			style:--rows={rows}
			class={[controlBox, 'grow-field py-2.5 leading-normal']}></textarea>
	{/snippet}
</Field>

<style>
	.grow-field {
		field-sizing: content;
		min-height: calc(var(--rows, 3) * 1.5em + 1.25rem);
		resize: vertical;
	}
</style>
