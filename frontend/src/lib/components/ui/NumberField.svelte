<script lang="ts">
	import type { HTMLInputAttributes } from 'svelte/elements';
	import { t } from '$lib/i18n';
	import { formatDecimal, parseDecimal } from './decimal';
	import Field from './Field.svelte';
	import { controlBox } from './variants';

	/**
	 * A number typed the Polish way: `"1,5"` → 1.5 (a dot works too). It is a text input with
	 * `inputmode="decimal"`, because `<input type="number">` rejects the comma in many browsers.
	 * `value` is `null` while the field is empty or holds text that is not a number; the field then
	 * shows its own error.
	 */
	type Props = Omit<HTMLInputAttributes, 'class' | 'value' | 'type' | 'min' | 'max'> & {
		label: string;
		value?: number | null;
		min?: number;
		max?: number;
		hint?: string;
		/** An error from outside (e.g. the API); shown when the text itself is valid. */
		error?: string | null;
		optional?: boolean;
		hideLabel?: boolean;
		/** A unit shown inside the box, e.g. "g". */
		suffix?: string;
		class?: string;
		/** Called with every valid change (also with `null` when cleared). */
		onValueChange?: (value: number | null) => void;
	};

	let {
		label,
		value = $bindable(null),
		min,
		max,
		hint,
		error = null,
		optional = false,
		hideLabel = false,
		suffix,
		class: className = '',
		id: idProp,
		onValueChange,
		onblur,
		...rest
	}: Props = $props();

	const uid = $props.id();
	let id = $derived(idProp ?? `nf-${uid}`);

	// What the user typed, while it differs from the printed `value`. `null` = show `value` itself,
	// so a value set from outside appears without any syncing effect.
	let draft = $state<string | null>(null);
	let text = $derived(draft ?? formatDecimal(value));

	function rangeError(n: number): string | null {
		if (min !== undefined && n < min) return t.common.ui.numberMin(formatDecimal(min));
		if (max !== undefined && n > max) return t.common.ui.numberMax(formatDecimal(max));
		return null;
	}

	function read(input: string): { value: number | null; error: string | null } {
		const parsed = parseDecimal(input);
		if (parsed.kind === 'invalid') return { value: null, error: t.common.ui.notANumber };
		if (parsed.kind === 'empty') return { value: null, error: null };
		const error = rangeError(parsed.value);
		return { value: error ? null : parsed.value, error };
	}

	let localError = $derived(draft === null ? null : read(draft).error);

	function handleInput(event: Event & { currentTarget: HTMLInputElement }) {
		draft = event.currentTarget.value;
		const next = read(draft).value;
		if (next !== (value ?? null)) {
			value = next;
			onValueChange?.(next);
		}
	}

	function handleBlur(event: FocusEvent & { currentTarget: HTMLInputElement }) {
		// Normalise "2," or "1.5" to the Polish form once the user leaves a valid field.
		if (!localError) draft = null;
		onblur?.(event);
	}
</script>

<Field {label} {hint} error={localError ?? error} {optional} {hideLabel} {id} class={className}>
	{#snippet control({ id, describedBy, invalid })}
		<div class="relative">
			<input
				{...rest}
				{id}
				type="text"
				inputmode="decimal"
				autocomplete="off"
				value={text}
				oninput={handleInput}
				onblur={handleBlur}
				required={!optional || undefined}
				aria-describedby={describedBy}
				aria-invalid={invalid || undefined}
				class={[controlBox, 'tabular', suffix && 'pr-12']}
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
