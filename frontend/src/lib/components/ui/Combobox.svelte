<script lang="ts" module>
	/** The value of the footer item that creates a new entry. Not a valid option value. */
	export const CREATE_VALUE = '__rl_create__';
</script>

<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import Search from '@lucide/svelte/icons/search';
	import { Combobox } from 'bits-ui';
	import { onDestroy } from 'svelte';
	import { t } from '$lib/i18n';
	import Field from './Field.svelte';
	import Spinner from './Spinner.svelte';
	import type { Option } from './types';
	import { controlBox } from './variants';

	/**
	 * Search-as-you-type picker over a server list (products, tags). Calls `search` after the user
	 * stops typing for `debounceMs`; an older request is aborted through its `AbortSignal`. When
	 * `onCreate` is given, a footer item "Dodaj „…”" offers to create what was typed (hidden when an
	 * option already has exactly that label).
	 */
	type Props = {
		label: string;
		search: (query: string, signal: AbortSignal) => Promise<Option[]>;
		/** The picked option, or `null`. */
		value?: Option | null;
		onValueChange?: (value: Option | null) => void;
		/** Called with the typed text when the footer item is chosen. */
		onCreate?: (query: string) => void;
		debounceMs?: number;
		minChars?: number;
		placeholder?: string;
		hint?: string;
		error?: string | null;
		optional?: boolean;
		hideLabel?: boolean;
		disabled?: boolean;
		class?: string;
	};

	let {
		label,
		search,
		value = $bindable(null),
		onValueChange,
		onCreate,
		debounceMs = 250,
		minChars = 1,
		placeholder = t.common.ui.searchPlaceholder,
		hint,
		error = null,
		optional = false,
		hideLabel = false,
		disabled = false,
		class: className = ''
	}: Props = $props();

	const uid = $props.id();
	let id = $derived(`cb-${uid}`);

	let open = $state(false);
	let query = $state('');
	let results = $state.raw<Option[]>([]);
	let loading = $state(false);
	let searched = $state(false);

	let timer: ReturnType<typeof setTimeout> | undefined;
	let controller: AbortController | undefined;

	let trimmed = $derived(query.trim());
	let canCreate = $derived(
		!!onCreate &&
			!loading &&
			trimmed.length > 0 &&
			!results.some((r) => r.label.toLocaleLowerCase('pl') === trimmed.toLocaleLowerCase('pl'))
	);
	let bitsItems = $derived(results.map((r) => ({ value: r.value, label: r.label })));

	onDestroy(() => {
		clearTimeout(timer);
		controller?.abort();
	});

	function runSearch(q: string) {
		controller?.abort();
		const current = new AbortController();
		controller = current;
		loading = true;
		search(q, current.signal)
			.then((items) => {
				if (current.signal.aborted) return;
				results = items;
			})
			.catch(() => {
				if (current.signal.aborted) return;
				results = [];
			})
			.finally(() => {
				if (current.signal.aborted) return;
				loading = false;
				searched = true;
			});
	}

	function handleInput(event: Event & { currentTarget: HTMLInputElement }) {
		query = event.currentTarget.value;
		clearTimeout(timer);
		if (value && query !== value.label) {
			value = null;
			onValueChange?.(null);
		}
		if (query.trim().length < minChars) {
			controller?.abort();
			results = [];
			loading = false;
			searched = false;
			return;
		}
		open = true;
		loading = true;
		const q = query.trim();
		timer = setTimeout(() => runSearch(q), debounceMs);
	}

	function handleValueChange(next: string) {
		if (next === CREATE_VALUE) {
			onCreate?.(trimmed);
			open = false;
			return;
		}
		const option = results.find((r) => r.value === next) ?? null;
		value = option;
		if (option) query = option.label;
		onValueChange?.(option);
	}
</script>

<Field {label} {hint} {error} {optional} {hideLabel} {id} class={className}>
	{#snippet control({ id, describedBy, invalid })}
		<Combobox.Root
			type="single"
			bind:open
			value={value?.value ?? ''}
			onValueChange={handleValueChange}
			inputValue={value?.label ?? query}
			items={bitsItems}
			{disabled}
		>
			<div class="relative">
				<Search
					size={20}
					aria-hidden="true"
					class="pointer-events-none absolute top-1/2 left-3 -translate-y-1/2 text-muted"
				/>
				<Combobox.Input
					{id}
					oninput={handleInput}
					{placeholder}
					autocomplete="off"
					aria-describedby={describedBy}
					aria-invalid={invalid || undefined}
					class={[controlBox, 'pr-10 pl-10']}
				/>
				{#if loading}
					<span class="absolute top-1/2 right-3 -translate-y-1/2 text-muted">
						<Spinner size={18} />
					</span>
				{/if}
			</div>
			<Combobox.Portal>
				<Combobox.Content
					sideOffset={6}
					class="z-50 max-h-[min(var(--bits-combobox-content-available-height),22rem)] w-(--bits-combobox-anchor-width) min-w-56 overflow-y-auto rounded-card border border-line bg-surface p-1 shadow-overlay"
				>
					{#each results as item (item.value)}
						<Combobox.Item
							value={item.value}
							label={item.label}
							disabled={item.disabled}
							class="flex min-h-11 cursor-pointer flex-col justify-center rounded-control px-3 text-base text-ink data-disabled:opacity-50 data-highlighted:bg-cobalt-wash"
						>
							<span>{item.label}</span>
							{#if item.hint}
								<span class="text-sm text-muted">{item.hint}</span>
							{/if}
						</Combobox.Item>
					{/each}
					{#if loading && results.length === 0}
						<p class="px-3 py-2.5 text-sm text-muted" role="status">{t.common.ui.searching}</p>
					{:else if searched && results.length === 0 && !canCreate}
						<p class="px-3 py-2.5 text-sm text-muted" role="status">{t.common.ui.noResults}</p>
					{/if}
					{#if canCreate}
						<Combobox.Item
							value={CREATE_VALUE}
							label={t.common.ui.createNew(trimmed)}
							class={[
								'flex min-h-11 cursor-pointer items-center gap-2 rounded-control px-3 font-semibold text-cobalt data-highlighted:bg-cobalt-wash',
								results.length > 0 && 'mt-1 border-t border-line'
							]}
						>
							<Plus size={20} aria-hidden="true" />
							{t.common.ui.createNew(trimmed)}
						</Combobox.Item>
					{/if}
				</Combobox.Content>
			</Combobox.Portal>
		</Combobox.Root>
	{/snippet}
</Field>
