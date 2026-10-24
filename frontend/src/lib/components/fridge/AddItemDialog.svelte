<script lang="ts">
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import ProductPicker from '$lib/components/catalog/ProductPicker.svelte';
	import {
		Button,
		Dialog,
		ErrorMessage,
		NumberField,
		Slider,
		TextField,
		toast
	} from '$lib/components/ui';
	import type { FridgeItem, ProductRef } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import { baseUnitLabel } from './amount';

	/**
	 * The "add item" sheet: pick (or create) a product, the package amount (prefilled from the
	 * product's default package) and how much is left (PROJECT §8.2). Mount it only while open.
	 */
	type Props = {
		open?: boolean;
		fridgeId: string;
		onAdded: (item: FridgeItem) => void;
	};

	let { open = $bindable(false), fridgeId, onAdded }: Props = $props();

	const labels = t.fridge.add;

	let product = $state<ProductRef | null>(null);
	let packageAmount = $state<number | null>(null);
	let percent = $state(100);
	let note = $state('');
	let saving = $state(false);
	let error = $state<unknown>(null);
	let submitted = $state(false);

	function productChanged(next: ProductRef | null) {
		product = next;
		if (next?.defaultPackageAmount) packageAmount = next.defaultPackageAmount;
	}

	let productError = $derived(submitted && !product ? labels.productRequired : null);
	let packageError = $derived(
		submitted && !(packageAmount !== null && packageAmount > 0) ? labels.packageRequired : null
	);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		submitted = true;
		if (!product || !(packageAmount !== null && packageAmount > 0) || saving) return;
		saving = true;
		error = null;
		try {
			const item = await unwrap(
				createApi().POST('/fridges/{id}/items', {
					params: { path: { id: fridgeId } },
					body: {
						productId: product.id,
						packageAmount,
						remainingPercent: percent,
						...(note.trim() ? { note: note.trim() } : {})
					}
				})
			);
			open = false;
			toast.success(labels.added);
			onAdded(item);
		} catch (e) {
			error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			saving = false;
		}
	}
</script>

<Dialog bind:open title={labels.title}>
	<form id="add-item-form" class="flex flex-col gap-5" onsubmit={submit}>
		<ErrorMessage {error} />
		<ProductPicker value={product} error={productError} onValueChange={productChanged} />
		<NumberField
			label={labels.packageLabel}
			hint={labels.packageHint}
			bind:value={packageAmount}
			min={0}
			suffix={product ? baseUnitLabel(product) : undefined}
			error={packageError}
		/>
		<Slider label={labels.percentLabel} bind:value={percent} min={5} />
		<TextField label={labels.noteLabel} bind:value={note} optional maxlength={200} />
	</form>
	{#snippet footer()}
		<Button variant="ghost" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button type="submit" form="add-item-form" loading={saving}>{labels.submit}</Button>
	{/snippet}
</Dialog>
