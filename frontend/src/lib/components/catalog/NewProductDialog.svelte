<script lang="ts">
	import { untrack } from 'svelte';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import type { components } from '$lib/api/schema';
	import {
		Button,
		Dialog,
		ErrorMessage,
		NumberField,
		Select,
		Switch,
		TextField,
		toast
	} from '$lib/components/ui';
	import type { DisplayClass, MeasureKind, ProductCategory, ProductRef } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	/**
	 * Mount it only while open (`{#if}`), so every use starts with a fresh form.
	 * The inline "Dodaj produkt" mini form of the ingredient row (PROJECT §7.1). Which optional fields
	 * appear depends on the measure kind: density for mass and volume, piece weight for every kind
	 * (it enables converting to pieces), sizes and halves only for countable products.
	 */
	type Props = {
		open?: boolean;
		/** The text typed in the product search; prefills the name. */
		name?: string;
		onCreated: (product: ProductRef) => void;
	};

	let { open = $bindable(false), name: initialName = '', onCreated }: Props = $props();

	const np = t.recipes.newProduct;
	const categoryItems = Object.entries(np.categories).map(([value, label]) => ({ value, label }));
	const kindItems = Object.entries(np.measureKinds).map(([value, label]) => ({ value, label }));
	const classesFor: Record<MeasureKind, DisplayClass[]> = {
		mass: ['weight'],
		volume: ['liquid', 'spice'],
		count: ['piece']
	};

	let name = $state(untrack(() => initialName));
	let category = $state<string>('other');
	let measureKind = $state<string>('mass');
	let displayClass = $state<string>('weight');
	let hasSizes = $state(false);
	let allowHalf = $state(false);
	let gramsPerMl = $state<number | null>(null);
	let gramsPerPiece = $state<number | null>(null);
	let defaultPackageAmount = $state<number | null>(null);
	let saving = $state(false);
	let error = $state<unknown>(null);

	let kind = $derived(measureKind as MeasureKind);
	let classItems = $derived(
		classesFor[kind].map((value) => ({ value, label: np.displayClasses[value] }))
	);
	let nameError = $derived(
		name.trim().length > 0 && (name.trim().length < 2 || name.trim().length > 80)
			? t.recipes.fieldErrors.out_of_range
			: null
	);

	function changeKind(next: string) {
		const allowed = classesFor[next as MeasureKind];
		if (!allowed.includes(displayClass as DisplayClass)) displayClass = allowed[0] ?? 'weight';
		if (next !== 'count') {
			hasSizes = false;
			allowHalf = false;
		}
	}

	async function submit(event?: SubmitEvent) {
		event?.preventDefault();
		if (name.trim().length < 2 || name.trim().length > 80) return;
		saving = true;
		error = null;
		const body: components['schemas']['ProductWrite'] = {
			name: name.trim(),
			category: category as ProductCategory,
			measureKind: kind,
			displayClass: displayClass as DisplayClass,
			hasSizes,
			allowHalf,
			...(gramsPerMl ? { gramsPerMl } : {}),
			...(gramsPerPiece ? { gramsPerPiece } : {}),
			...(defaultPackageAmount ? { defaultPackageAmount } : {})
		};
		const api = createApi();
		try {
			const product = await unwrap(api.POST('/products', { body }));
			toast.success(np.created(product.name));
			open = false;
			onCreated(product);
		} catch (e) {
			// A duplicate answers with the existing product's id: use that product instead.
			const existingId =
				e instanceof ApiError && e.code === 'product.duplicate' ? e.problem?.existingId : null;
			if (typeof existingId === 'string') {
				try {
					const product = await unwrap(
						api.GET('/products/{id}', { params: { path: { id: existingId } } })
					);
					toast.show(np.existing(product.name));
					open = false;
					onCreated(product);
					return;
				} catch (inner) {
					error = inner;
				}
			} else {
				error = e;
			}
		} finally {
			saving = false;
		}
	}
</script>

<Dialog bind:open title={np.title}>
	<form id="new-product-form" class="flex flex-col gap-5" onsubmit={submit}>
		<ErrorMessage {error} />
		<TextField label={np.name} bind:value={name} error={nameError} maxlength={80} />
		<Select label={np.category} items={categoryItems} bind:value={category} />
		<Select
			label={np.measureKind}
			items={kindItems}
			bind:value={measureKind}
			onValueChange={changeKind}
		/>
		<Select label={np.displayClass} items={classItems} bind:value={displayClass} />
		{#if kind !== 'count'}
			<NumberField
				label={np.gramsPerMl}
				hint={np.gramsPerMlHint}
				bind:value={gramsPerMl}
				min={0}
				optional
				suffix="g/ml"
			/>
		{/if}
		<NumberField
			label={np.gramsPerPiece}
			hint={np.gramsPerPieceHint}
			bind:value={gramsPerPiece}
			min={0}
			optional
			suffix="g"
		/>
		{#if kind === 'count'}
			<Switch label={np.hasSizes} bind:checked={hasSizes} />
			<Switch label={np.allowHalf} bind:checked={allowHalf} />
		{/if}
		<NumberField
			label={np.defaultPackageAmount}
			bind:value={defaultPackageAmount}
			min={0}
			optional
			suffix={kind === 'mass' ? 'g' : kind === 'volume' ? 'ml' : undefined}
		/>
	</form>
	{#snippet footer()}
		<Button variant="secondary" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button type="submit" form="new-product-form" loading={saving}>{np.submit}</Button>
	{/snippet}
</Dialog>
