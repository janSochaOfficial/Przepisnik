<script lang="ts">
	import { t } from '$lib/i18n';
	import Chip from './Chip.svelte';
	import Field from './Field.svelte';
	import { controlBox } from './variants';

	/**
	 * A list of short free-text values (recipe tags, utensils). Enter or a comma adds the typed text;
	 * Backspace in an empty input removes the last value; each chip has its own remove button.
	 * Values are trimmed and de-duplicated case-insensitively. `suggestions` feed a native datalist.
	 */
	type Props = {
		label: string;
		value?: string[];
		suggestions?: string[];
		placeholder?: string;
		hint?: string;
		error?: string | null;
		optional?: boolean;
		maxLength?: number;
		disabled?: boolean;
		class?: string;
		onValueChange?: (value: string[]) => void;
	};

	let {
		label,
		value = $bindable([]),
		suggestions = [],
		placeholder,
		hint = t.common.ui.tagInputHint,
		error = null,
		optional = true,
		maxLength = 40,
		disabled = false,
		class: className = '',
		onValueChange
	}: Props = $props();

	const uid = $props.id();
	let id = $derived(`tag-${uid}`);
	let draft = $state('');

	function set(next: string[]) {
		value = next;
		onValueChange?.(next);
	}

	function commit() {
		const text = draft.replace(/,/g, ' ').trim().replace(/\s+/g, ' ');
		draft = '';
		if (!text) return;
		const lower = text.toLocaleLowerCase('pl');
		if (value.some((v) => v.toLocaleLowerCase('pl') === lower)) return;
		set([...value, text]);
	}

	function handleKeydown(event: KeyboardEvent) {
		if (event.key === 'Enter' || event.key === ',') {
			event.preventDefault();
			commit();
		} else if (event.key === 'Backspace' && draft === '' && value.length > 0) {
			set(value.slice(0, -1));
		}
	}
</script>

<Field {label} {hint} {error} {optional} {id} class={className}>
	{#snippet control({ id, describedBy, invalid })}
		<div
			class={[
				controlBox,
				'flex flex-wrap items-center gap-1.5 py-1.5',
				'has-[input:focus-visible]:outline-3 has-[input:focus-visible]:outline-offset-2 has-[input:focus-visible]:outline-cobalt'
			]}
			aria-invalid={invalid || undefined}
		>
			{#if value.length > 0}
				<ul class="contents">
					{#each value as tag (tag)}
						<li>
							<Chip text={tag} {disabled} onRemove={() => set(value.filter((v) => v !== tag))}
								>{tag}</Chip
							>
						</li>
					{/each}
				</ul>
			{/if}
			<input
				{id}
				bind:value={draft}
				{placeholder}
				{disabled}
				maxlength={maxLength}
				list={suggestions.length > 0 ? `${id}-list` : undefined}
				enterkeyhint="enter"
				autocomplete="off"
				aria-describedby={describedBy}
				onkeydown={handleKeydown}
				onblur={commit}
				class="min-h-9 min-w-24 flex-1 bg-transparent text-base outline-none"
			/>
			{#if suggestions.length > 0}
				<datalist id={`${id}-list`}>
					{#each suggestions as suggestion (suggestion)}
						<option value={suggestion}></option>
					{/each}
				</datalist>
			{/if}
		</div>
	{/snippet}
</Field>
