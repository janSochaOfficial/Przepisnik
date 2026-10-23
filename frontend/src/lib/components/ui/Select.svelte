<script lang="ts">
	import Check from '@lucide/svelte/icons/check';
	import ChevronDown from '@lucide/svelte/icons/chevron-down';
	import { Select } from 'bits-ui';
	import { t } from '$lib/i18n';
	import Field from './Field.svelte';
	import type { Option } from './types';
	import { controlBox } from './variants';

	/** Pick one of a short, fixed list (≤ ~12 options). For long or searched lists use `Combobox`. */
	type Props = {
		label: string;
		items: Option[];
		value?: string;
		placeholder?: string;
		hint?: string;
		error?: string | null;
		optional?: boolean;
		hideLabel?: boolean;
		disabled?: boolean;
		name?: string;
		class?: string;
		onValueChange?: (value: string) => void;
	};

	let {
		label,
		items,
		value = $bindable(''),
		placeholder = t.common.ui.selectPlaceholder,
		hint,
		error = null,
		optional = false,
		hideLabel = false,
		disabled = false,
		name,
		class: className = '',
		onValueChange
	}: Props = $props();

	const uid = $props.id();
	let id = $derived(`sel-${uid}`);
	let selected = $derived(items.find((item) => item.value === value));
</script>

<Field {label} {hint} {error} {optional} {hideLabel} {id} class={className}>
	{#snippet control({ id, describedBy, invalid })}
		<Select.Root type="single" bind:value {items} {disabled} {name} {onValueChange}>
			<Select.Trigger
				{id}
				aria-describedby={describedBy}
				aria-invalid={invalid || undefined}
				class={[controlBox, 'flex items-center justify-between gap-2 text-left']}
			>
				<span class={['truncate', !selected && 'text-muted']}>
					{selected?.label ?? placeholder}
				</span>
				<ChevronDown size={20} aria-hidden="true" class="shrink-0 text-muted" />
			</Select.Trigger>
			<Select.Portal>
				<Select.Content
					sideOffset={6}
					class="z-50 max-h-[min(var(--bits-select-content-available-height),20rem)] w-(--bits-select-anchor-width) min-w-48 overflow-hidden rounded-card border border-line bg-surface p-1 shadow-overlay"
				>
					<Select.Viewport>
						{#each items as item (item.value)}
							<Select.Item
								value={item.value}
								label={item.label}
								disabled={item.disabled}
								class="flex min-h-11 cursor-pointer items-center gap-3 rounded-control px-3 text-base text-ink data-disabled:opacity-50 data-highlighted:bg-cobalt-wash"
							>
								{#snippet children({ selected })}
									<span class="flex-1">
										{item.label}
										{#if item.hint}
											<span class="block text-sm text-muted">{item.hint}</span>
										{/if}
									</span>
									{#if selected}
										<Check size={20} aria-hidden="true" class="text-cobalt" />
									{/if}
								{/snippet}
							</Select.Item>
						{/each}
					</Select.Viewport>
				</Select.Content>
			</Select.Portal>
		</Select.Root>
	{/snippet}
</Field>
