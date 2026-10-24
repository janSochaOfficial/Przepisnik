<script lang="ts">
	import { Select } from '$lib/components/ui';
	import type { CustomUnitRef, ProductRef, Unit } from '$lib/domain/types';
	import { customUnitFits } from '$lib/domain/units';
	import NewUnitDialog from './NewUnitDialog.svelte';
	import { NEW_UNIT_VALUE, parseUnitValue, unitOptions, unitValue } from './options';
	import { loadUnits } from './units-client';

	/**
	 * The unit picker of the recipe and variant editors and the "Mam:" field (PROJECT §5.6): the
	 * built-in units valid for the product, then its custom units, then "+ Nowa jednostka…", which
	 * opens `NewUnitDialog` and selects the new unit.
	 */
	type Props = {
		label: string;
		product: ProductRef | null;
		unit: Unit | null;
		customUnit: CustomUnitRef | null;
		error?: string | null;
		class?: string;
		/** Hides "+ Nowa jednostka…". */
		allowNew?: boolean;
		onChange?: (unit: Unit, customUnit: CustomUnitRef | null) => void;
	};

	let {
		label,
		product,
		unit = $bindable(),
		customUnit = $bindable(),
		error = null,
		class: className = '',
		allowNew = true,
		onChange
	}: Props = $props();

	let loaded = $state<CustomUnitRef[]>([]);
	let dialogOpen = $state(false);
	let selected = $derived(unitValue(unit, customUnit));

	$effect(() => {
		const productId = product?.id ?? null;
		let cancelled = false;
		loadUnits(productId).then(
			(list) => {
				if (!cancelled) loaded = list;
			},
			() => {
				// Without the catalog the built-in units still work.
				if (!cancelled) loaded = [];
			}
		);
		return () => {
			cancelled = true;
		};
	});

	let items = $derived(unitOptions(product, loaded, customUnit, allowNew));

	function choose(next: Unit, nextCustom: CustomUnitRef | null) {
		unit = next;
		customUnit = nextCustom;
		onChange?.(next, nextCustom);
	}

	function valueChanged(value: string) {
		if (value === NEW_UNIT_VALUE) {
			// Keep showing the current unit; the dialog decides what comes next.
			selected = unitValue(unit, customUnit);
			dialogOpen = true;
			return;
		}
		const choice = parseUnitValue(value, [...loaded, ...(customUnit ? [customUnit] : [])]);
		if (choice) choose(choice.unit, choice.customUnit);
	}

	function created(newUnit: CustomUnitRef) {
		if (!loaded.some((u) => u.id === newUnit.id)) loaded = [...loaded, newUnit];
		if (!product || customUnitFits(newUnit, product)) choose('custom', newUnit);
	}
</script>

<Select
	{label}
	{items}
	bind:value={selected}
	{error}
	class={className}
	onValueChange={valueChanged}
/>
{#if dialogOpen}
	<NewUnitDialog bind:open={dialogOpen} {product} onCreated={created} />
{/if}
