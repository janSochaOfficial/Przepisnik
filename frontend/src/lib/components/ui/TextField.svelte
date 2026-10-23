<script lang="ts">
	import type { HTMLInputAttributes } from 'svelte/elements';
	import Field from './Field.svelte';
	import { controlBox } from './variants';

	type Props = Omit<HTMLInputAttributes, 'class' | 'value' | 'type'> & {
		label: string;
		value?: string;
		type?: 'text' | 'email' | 'search' | 'url' | 'password' | 'tel';
		hint?: string;
		error?: string | null;
		optional?: boolean;
		hideLabel?: boolean;
		/** Short text inside the box after the value, e.g. a unit. */
		suffix?: string;
		class?: string;
	};

	let {
		label,
		value = $bindable(''),
		type = 'text',
		hint,
		error = null,
		optional = false,
		hideLabel = false,
		suffix,
		class: className = '',
		id: idProp,
		...rest
	}: Props = $props();

	const uid = $props.id();
	let id = $derived(idProp ?? `tf-${uid}`);
</script>

<Field {label} {hint} {error} {optional} {hideLabel} {id} class={className}>
	{#snippet control({ id, describedBy, invalid })}
		<div class="relative">
			<input
				{...rest}
				{id}
				{type}
				bind:value
				required={!optional || undefined}
				aria-describedby={describedBy}
				aria-invalid={invalid || undefined}
				class={[controlBox, suffix && 'pr-12']}
			/>
			{#if suffix}
				<span
					class="pointer-events-none absolute inset-y-0 right-3 flex items-center text-muted"
					aria-hidden="true">{suffix}</span
				>
			{/if}
		</div>
	{/snippet}
</Field>
