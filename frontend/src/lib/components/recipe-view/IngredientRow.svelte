<script lang="ts">
	import Check from '@lucide/svelte/icons/check';
	import Heart from '@lucide/svelte/icons/heart';
	import Pin from '@lucide/svelte/icons/pin';
	import { compatibleUnits, defaultUnit } from '$lib/components/recipe-editor/draft';
	import { BottomSheet, Button, NumberField, Select } from '$lib/components/ui';
	import { formatIngredientAmount } from '$lib/domain/format';
	import type { RankedOption } from '$lib/domain/matching';
	import type { PieceSize, Unit } from '$lib/domain/types';
	import { toBaseAmount } from '$lib/domain/units';
	import { t } from '$lib/i18n';
	import type { SelectionState } from '$lib/state/selection.svelte';
	import type { SlotRow } from './rows';

	/**
	 * One ingredient of the recipe view. Tapping it opens the sheet with the ranked options of its
	 * slot (Warianty) and the "Mam: …" scaling field (Przelicz).
	 */
	type Props = {
		row: SlotRow;
		state: SelectionState;
		/** False for archived recipes (D-27): the "create a variant" entry is hidden. */
		canCreateVariant: boolean;
		oncreate: () => void;
	};

	let { row, state: selection, canCreateVariant, oncreate }: Props = $props();

	const labels = t.variants;
	const units = t.recipes.ingredients;

	let open = $state(false);
	let ingredient = $derived(row.ingredient);
	let product = $derived(row.ingredient?.product ?? null);
	let options = $derived(selection.match.options[row.slotKey] ?? []);
	let formatOptions = $derived({ liquidUnits: selection.liquidUnits });

	let amount = $derived(
		ingredient ? formatIngredientAmount(ingredient, selection.factor, formatOptions) : null
	);
	let amountText = $derived(
		amount ? `${amount.approximate ? '~' : ''}${amount.text}` : labels.view.toTaste
	);

	let canScale = $derived(
		ingredient !== null && ingredient.quantity !== null && (ingredient.baseAmount ?? 0) > 0
	);

	// "Mam: …" form; it starts over whenever the sheet opens.
	let have = $state<number | null>(null);
	let unit = $state<string>('g');
	let size = $state<string>('medium');
	let scaleError = $state<string | null>(null);

	let unitItems = $derived(
		compatibleUnits(product).map((u) => ({ value: u, label: units.unitLabels[u] }))
	);
	let showSize = $derived(unit === 'piece' && product?.hasSizes === true);
	const sizeItems = (['small', 'medium', 'large'] as const).map((s) => ({
		value: s,
		label: units.sizeLabels[s]
	}));

	function onOpenChange(next: boolean) {
		if (!next) return;
		have = null;
		scaleError = null;
		if (product) unit = ingredient?.quantity?.unit ?? defaultUnit(product);
		size = ingredient?.quantity?.size ?? 'medium';
	}

	function applyScale() {
		if (!product || have === null || !(have > 0)) {
			scaleError = labels.sheet.enterAmount;
			return;
		}
		const base = toBaseAmount(
			{ value: have, unit: unit as Unit, ...(showSize ? { size: size as PieceSize } : {}) },
			product
		);
		if (base === null || !(base > 0)) {
			scaleError = labels.sheet.incompatible;
			return;
		}
		selection.setScaling(row.slotKey, base);
		open = false;
	}

	function optionName(ranked: RankedOption): string {
		const ing = ranked.option.ingredient;
		if (!ing) return labels.sheet.none;
		const a = formatIngredientAmount(ing, selection.factor, formatOptions);
		return `${a ? `${a.approximate ? '~' : ''}${a.text} ` : ''}${ing.product.name}`;
	}

	function supporters(ranked: RankedOption) {
		return ranked.option.variantIds.map((id) => {
			const v = selection.matrix.variants.find((m) => m.id === id);
			return {
				id,
				name: id === 'base' ? labels.view.baseName : (v?.name ?? ''),
				favorite: v?.isFavorite ?? false
			};
		});
	}

	// In a custom mix no single variant is compatible, so nothing is dimmed (the ranking is by support).
	function dimmed(ranked: RankedOption): boolean {
		return !selection.match.isCustomMix && !ranked.compatible;
	}

	function choose(optionKey: string) {
		selection.pick(row.slotKey, optionKey);
		open = false;
	}

	let removedName = $derived(row.slot.label);
	let title = $derived(labels.sheet.title(product?.name ?? removedName));
</script>

<li class="border-b border-line last:border-b-0">
	<BottomSheet bind:open {title} {onOpenChange}>
		{#snippet trigger({ props })}
			<button
				{...props}
				type="button"
				class="flex min-h-11 w-full items-start gap-3 rounded-control py-2 text-left hover:bg-cobalt-wash"
			>
				{#if ingredient}
					<span class="tabular w-28 shrink-0 font-semibold">{amountText}</span>
					<span class="flex-1">
						<span>{ingredient.product.name}</span>
						{#if ingredient.note}<span class="text-muted">, {ingredient.note}</span>{/if}
						{#if ingredient.optional}
							<span class="text-sm text-muted">({labels.view.optional})</span>
						{/if}
						{#if row.pick.source === 'user'}
							<span class="ml-1 inline-flex items-center gap-1 text-sm text-cobalt">
								<Pin size={14} aria-hidden="true" />{labels.view.marker.user}
							</span>
						{:else if row.changed}
							<span class="ml-1 text-sm text-muted">{labels.view.marker.auto}</span>
						{/if}
					</span>
				{:else}
					<span class="flex-1 text-muted line-through">
						{removedName}
						<span class="text-sm no-underline">({labels.view.removed})</span>
					</span>
				{/if}
			</button>
		{/snippet}

		<div class="flex flex-col gap-5">
			<section aria-labelledby="opts-{row.slotKey}">
				<h3 id="opts-{row.slotKey}" class="text-base">{labels.sheet.variantsHeading}</h3>
				<ul class="mt-2 flex flex-col">
					{#each options as ranked (ranked.option.key)}
						<li>
							<button
								type="button"
								aria-pressed={ranked.selected}
								class={[
									'flex min-h-11 w-full items-start gap-2 rounded-control px-2 py-2 text-left hover:bg-cobalt-wash',
									ranked.selected && 'bg-cobalt-wash',
									dimmed(ranked) && 'text-muted'
								]}
								onclick={() => choose(ranked.option.key)}
							>
								<span class="mt-0.5 size-5 shrink-0">
									{#if ranked.selected}
										<Check size={20} aria-hidden="true" /><span class="sr-only"
											>{labels.sheet.selected}</span
										>
									{/if}
								</span>
								<span class="flex-1">
									<span class="block font-semibold">{optionName(ranked)}</span>
									<span class="block text-sm text-muted">
										{labels.sheet.supportedBy}
										{#each supporters(ranked) as s, i (s.id)}
											{#if i > 0},
											{/if}{s.name}{#if s.favorite}
												<Heart
													size={13}
													class="inline text-cobalt"
													fill="currentColor"
													aria-label={labels.sheet.favoriteVariant}
												/>{/if}
										{/each}
									</span>
								</span>
							</button>
						</li>
					{/each}
				</ul>
				{#if canCreateVariant}
					<button
						type="button"
						class="mt-2 flex min-h-11 w-full items-center rounded-control px-2 py-2 text-left font-semibold text-cobalt hover:bg-cobalt-wash"
						onclick={() => {
							open = false;
							oncreate();
						}}
					>
						{labels.sheet.createFromSelection}
					</button>
				{/if}
			</section>

			{#if canScale}
				<section aria-labelledby="scale-{row.slotKey}" class="border-t border-line pt-4">
					<h3 id="scale-{row.slotKey}" class="text-base">{labels.sheet.scaleHeading}</h3>
					<form
						class="mt-2 flex flex-col gap-3"
						onsubmit={(e) => {
							e.preventDefault();
							applyScale();
						}}
					>
						<NumberField label={labels.sheet.have} bind:value={have} min={0} error={scaleError} />
						<div class="flex gap-3">
							<Select
								class="flex-1"
								label={labels.sheet.haveUnit}
								items={unitItems}
								bind:value={unit}
							/>
							{#if showSize}
								<Select
									class="flex-1"
									label={labels.sheet.haveSize}
									items={sizeItems}
									bind:value={size}
								/>
							{/if}
						</div>
						<div class="flex gap-2">
							<Button type="submit">{labels.sheet.apply}</Button>
							{#if selection.scaling}
								<Button
									variant="secondary"
									onclick={() => {
										selection.resetScaling();
										open = false;
									}}
								>
									{labels.sheet.reset}
								</Button>
							{/if}
						</div>
					</form>
				</section>
			{/if}
		</div>
	</BottomSheet>
</li>
