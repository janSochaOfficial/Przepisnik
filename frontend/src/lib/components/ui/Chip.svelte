<script lang="ts">
	import X from '@lucide/svelte/icons/x';
	import type { Snippet } from 'svelte';
	import { t } from '$lib/i18n';

	/**
	 * A small pill. Three uses:
	 * - static label (no `onclick`, no `onRemove`), e.g. a tag on a recipe card;
	 * - toggle filter (`selected` + `onclick`), rendered as a button with `aria-pressed`;
	 * - removable value (`onRemove`), e.g. inside `TagInput`.
	 */
	type Props = {
		selected?: boolean;
		onclick?: () => void;
		onRemove?: () => void;
		/** The plain text of the chip, used for the remove button's label. */
		text?: string;
		disabled?: boolean;
		class?: string;
		children: Snippet;
	};

	let {
		selected = false,
		onclick,
		onRemove,
		text = '',
		disabled = false,
		class: className = '',
		children
	}: Props = $props();

	const pill =
		'inline-flex min-h-9 items-center gap-1 rounded-full border px-3 text-sm font-semibold transition-colors duration-150';
</script>

{#if onclick}
	<!-- A 36 px pill with an invisible 44 px hit area (DESIGN.md "Touch"). -->
	<button
		type="button"
		{disabled}
		aria-pressed={selected}
		{onclick}
		class={[
			pill,
			'relative before:absolute before:-inset-y-1 before:inset-x-0 disabled:opacity-55',
			selected
				? 'border-cobalt bg-cobalt text-on-cobalt'
				: 'border-line-strong bg-surface text-ink hover:bg-cobalt-wash',
			className
		]}
	>
		{@render children()}
	</button>
{:else}
	<span
		class={[
			pill,
			selected ? 'border-cobalt bg-cobalt-wash text-ink' : 'border-line bg-surface text-ink',
			onRemove && 'pr-0.5',
			className
		]}
	>
		{@render children()}
		{#if onRemove}
			<button
				type="button"
				{disabled}
				class="relative -my-1 inline-flex size-8 items-center justify-center rounded-full text-muted before:absolute before:-inset-1.5 hover:bg-sunken hover:text-ink"
				aria-label={t.common.ui.removeTag(text)}
				onclick={onRemove}
			>
				<X size={16} aria-hidden="true" />
			</button>
		{/if}
	</span>
{/if}
