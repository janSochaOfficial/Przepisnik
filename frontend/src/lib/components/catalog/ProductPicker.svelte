<script lang="ts">
	import { untrack } from 'svelte';
	import { createApi, unwrap } from '$lib/api/client';
	import { Combobox, ConfirmDialog, type Option } from '$lib/components/ui';
	import type { ProductRef } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import NewProductDialog from './NewProductDialog.svelte';
	import { sameProductName } from './product-name';

	/**
	 * Server-backed product search (`GET /products?q=`) with an inline "Dodaj produkt" footer that
	 * opens the mini form. `value` is the whole `ProductRef`, so the editor can check unit
	 * compatibility without another request. With `confirmNew`, leaving the field (or Enter) with
	 * text that is not a picked product selects the product of that exact name, or asks whether to
	 * add it to the catalog (PROJECT §4.2, D-32).
	 */
	type Props = {
		value?: ProductRef | null;
		label?: string;
		error?: string | null;
		hideLabel?: boolean;
		confirmNew?: boolean;
		onValueChange?: (product: ProductRef | null) => void;
	};

	let {
		value = $bindable(null),
		label = t.recipes.ingredients.product,
		error = null,
		hideLabel = false,
		confirmNew = false,
		onValueChange
	}: Props = $props();

	const known: Record<string, ProductRef> = {};
	// The initial product only seeds the input text.
	let option = $state<Option | null>(
		untrack(() => (value ? { value: value.id, label: value.name } : null))
	);
	let creating = $state(false);
	let asking = $state(false);
	// "Dodaj do bazy" closes the prompt too; that close is not a "Nie dodawaj".
	let adding = false;
	let draftName = $state('');
	let combobox: { clear: () => void; focus: () => void } | undefined = $state();

	function remember(product: ProductRef): Option {
		known[product.id] = product;
		const category = t.recipes.newProduct.categories[product.category];
		return {
			value: product.id,
			label: product.name,
			hint: product.isVerified ? category : `${category} · ${t.recipes.newProduct.userAdded}`
		};
	}

	async function search(q: string, signal?: AbortSignal): Promise<Option[]> {
		const products = await unwrap(
			createApi().GET('/products', { params: { query: { q, limit: 20 } }, signal })
		);
		return products.map(remember);
	}

	function select(product: ProductRef) {
		option = remember(product);
		value = product;
		onValueChange?.(product);
	}

	function handleChange(next: Option | null) {
		const product = next ? (known[next.value] ?? null) : null;
		value = product;
		onValueChange?.(product);
	}

	async function commit(text: string) {
		if (asking || creating) return;
		let options: Option[];
		try {
			options = await search(text);
		} catch {
			return; // Offline or failing: leave the text alone rather than nag.
		}
		if (value) return;
		const match = options.find((o) => sameProductName(o.label, text));
		if (match) {
			const product = known[match.value];
			if (product) select(product);
			return;
		}
		draftName = text;
		adding = false;
		asking = true;
	}

	function decline() {
		if (adding) {
			adding = false;
			return;
		}
		combobox?.clear();
		option = null;
		// The dialog gives focus back to its own trigger/body when it unmounts; take it afterwards.
		setTimeout(() => combobox?.focus(), 50);
	}
</script>

<Combobox
	bind:this={combobox}
	{label}
	{hideLabel}
	{error}
	{search}
	bind:value={option}
	placeholder={t.recipes.ingredients.productPlaceholder}
	onValueChange={handleChange}
	onCreate={(query) => {
		draftName = query;
		creating = true;
	}}
	onCommit={confirmNew ? commit : undefined}
/>
{#if asking}
	<ConfirmDialog
		bind:open={asking}
		tone="primary"
		title={t.recipes.newProduct.unknownTitle(draftName)}
		description={t.recipes.newProduct.unknownDescription}
		confirmLabel={t.recipes.newProduct.unknownAdd}
		cancelLabel={t.recipes.newProduct.unknownDecline}
		onConfirm={() => {
			adding = true;
			creating = true;
		}}
		onCancel={decline}
	/>
{/if}
{#if creating}
	<NewProductDialog bind:open={creating} name={draftName} onCreated={select} />
{/if}
