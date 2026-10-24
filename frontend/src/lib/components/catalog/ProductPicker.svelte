<script lang="ts">
	import { untrack } from 'svelte';
	import { createApi, unwrap } from '$lib/api/client';
	import { Combobox, type Option } from '$lib/components/ui';
	import type { ProductRef } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import NewProductDialog from './NewProductDialog.svelte';

	/**
	 * Server-backed product search (`GET /products?q=`) with an inline "Dodaj produkt" footer that
	 * opens the mini form. `value` is the whole `ProductRef`, so the editor can check unit
	 * compatibility without another request.
	 */
	type Props = {
		value?: ProductRef | null;
		label?: string;
		error?: string | null;
		hideLabel?: boolean;
		onValueChange?: (product: ProductRef | null) => void;
	};

	let {
		value = $bindable(null),
		label = t.recipes.ingredients.product,
		error = null,
		hideLabel = false,
		onValueChange
	}: Props = $props();

	const known: Record<string, ProductRef> = {};
	// The initial product only seeds the input text.
	let option = $state<Option | null>(
		untrack(() => (value ? { value: value.id, label: value.name } : null))
	);
	let creating = $state(false);
	let draftName = $state('');

	function remember(product: ProductRef): Option {
		known[product.id] = product;
		return {
			value: product.id,
			label: product.name,
			hint: t.recipes.newProduct.categories[product.category]
		};
	}

	async function search(q: string, signal: AbortSignal): Promise<Option[]> {
		const products = await unwrap(
			createApi().GET('/products', { params: { query: { q, limit: 20 } }, signal })
		);
		return products.map(remember);
	}

	function handleChange(next: Option | null) {
		const product = next ? (known[next.value] ?? null) : null;
		value = product;
		onValueChange?.(product);
	}

	function created(product: ProductRef) {
		option = remember(product);
		value = product;
		onValueChange?.(product);
	}
</script>

<Combobox
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
/>
{#if creating}
	<NewProductDialog bind:open={creating} name={draftName} onCreated={created} />
{/if}
