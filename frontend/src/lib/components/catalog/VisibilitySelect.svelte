<script lang="ts">
	import Globe from '@lucide/svelte/icons/globe';
	import Lock from '@lucide/svelte/icons/lock';
	import Refrigerator from '@lucide/svelte/icons/refrigerator';
	import { ConfirmDialog } from '$lib/components/ui';
	import type { IconComponent } from '$lib/components/ui';
	import type { Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	/**
	 * Picks who sees a recipe or variant (PROJECT §4.4): three options with an icon and a one-line
	 * explanation. Choosing "Publiczny" first asks for confirmation (turn off with `confirmPublic={false}`
	 * when the caller asks itself). `hint` adds a note under the options (e.g. the recipe is narrower).
	 */
	type Props = {
		value?: Visibility;
		label?: string;
		hint?: string;
		disabled?: boolean;
		confirmPublic?: boolean;
		onValueChange?: (value: Visibility) => void;
	};

	let {
		value = $bindable('private'),
		label = t.recipes.visibility.label,
		hint,
		disabled = false,
		confirmPublic = true,
		onValueChange
	}: Props = $props();

	const uid = $props.id();
	const options: { value: Visibility; label: string; hint: string; icon: IconComponent }[] = [
		{
			value: 'private',
			label: t.recipes.visibility.private,
			hint: t.recipes.visibility.privateHint,
			icon: Lock
		},
		{
			value: 'fridges',
			label: t.recipes.visibility.fridges,
			hint: t.recipes.visibility.fridgesHint,
			icon: Refrigerator
		},
		{
			value: 'public',
			label: t.recipes.visibility.public,
			hint: t.recipes.visibility.publicHint,
			icon: Globe
		}
	];

	let confirming = $state(false);

	/** Turns a click on "Publiczny" into a confirmation; the radio stays where it was. */
	function guardPublic(event: MouseEvent, next: Visibility) {
		if (next === 'public' && confirmPublic && value !== 'public') {
			event.preventDefault();
			confirming = true;
		}
	}

	function apply(next: Visibility) {
		value = next;
		onValueChange?.(next);
	}
</script>

<fieldset class="flex flex-col gap-2" {disabled}>
	<legend class="mb-1.5 text-sm font-semibold text-ink">{label}</legend>
	{#each options as option (option.value)}
		{@const Icon = option.icon}
		<label
			class={[
				'flex min-h-14 cursor-pointer items-center gap-3 rounded-control border px-3 py-2 transition-colors duration-150 has-[input:focus-visible]:outline-3 has-[input:focus-visible]:outline-offset-2 has-[input:focus-visible]:outline-cobalt',
				value === option.value
					? 'border-cobalt bg-cobalt-wash'
					: 'border-line-strong bg-surface hover:bg-cobalt-wash'
			]}
		>
			<input
				type="radio"
				name={`visibility-${uid}`}
				value={option.value}
				checked={value === option.value}
				onclick={(event) => guardPublic(event, option.value)}
				onchange={() => apply(option.value)}
				class="size-5 shrink-0 accent-cobalt"
			/>
			<Icon size={22} aria-hidden="true" class="shrink-0 text-cobalt" />
			<span class="flex flex-col">
				<span class="text-base font-semibold text-ink">{option.label}</span>
				<span class="text-sm text-muted">{option.hint}</span>
			</span>
		</label>
	{/each}
	{#if hint}
		<p class="text-sm text-muted">{hint}</p>
	{/if}
</fieldset>

<ConfirmDialog
	bind:open={confirming}
	tone="primary"
	title={t.recipes.visibility.confirmPublicTitle}
	description={t.recipes.visibility.confirmPublicText}
	confirmLabel={t.recipes.visibility.confirmPublic}
	onConfirm={() => apply('public')}
/>
