<script lang="ts">
	import type { Snippet } from 'svelte';
	import { t } from '$lib/i18n';

	/**
	 * Label, hint and error around one control. Used by every form primitive; feature code uses it
	 * directly only for a custom control. The control gets `id`, `aria-describedby` and
	 * `aria-invalid` from the `control` snippet argument.
	 */
	type Props = {
		label: string;
		/** Hides the label visually (it stays for screen readers). */
		hideLabel?: boolean;
		hint?: string;
		error?: string | null;
		/** Adds "(opcjonalnie)" after the label. Required fields are the default, so they get no mark. */
		optional?: boolean;
		id: string;
		/** Use `false` for controls that label themselves through `aria-labelledby` (bits-ui triggers). */
		labelFor?: boolean;
		class?: string;
		control: Snippet<[{ id: string; describedBy: string | undefined; invalid: boolean }]>;
	};

	let {
		label,
		hideLabel = false,
		hint,
		error = null,
		optional = false,
		id,
		labelFor = true,
		class: className = '',
		control
	}: Props = $props();

	let hintId = $derived(`${id}-hint`);
	let errorId = $derived(`${id}-error`);
	let describedBy = $derived(
		[error ? errorId : null, hint ? hintId : null].filter(Boolean).join(' ') || undefined
	);
</script>

<div class={['flex flex-col gap-1.5', className]}>
	<label
		id={`${id}-label`}
		for={labelFor ? id : undefined}
		class={['text-sm font-semibold text-ink', hideLabel && 'sr-only']}
	>
		{label}
		{#if optional}
			<span class="font-normal text-muted">({t.common.ui.optional})</span>
		{/if}
	</label>
	{@render control({ id, describedBy, invalid: !!error })}
	{#if error}
		<p id={errorId} class="text-sm font-semibold text-beet">{error}</p>
	{/if}
	{#if hint}
		<p id={hintId} class="text-sm text-muted">{hint}</p>
	{/if}
</div>
