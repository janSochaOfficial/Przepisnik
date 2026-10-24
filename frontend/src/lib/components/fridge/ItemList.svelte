<script lang="ts">
	import ChevronRight from '@lucide/svelte/icons/chevron-right';
	import type { FridgeItem, LiquidUnits } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import { inventoryText, remainingText } from './amount';
	import { groupByCategory } from './group';

	/** The fridge's items grouped by category; each row is one package and opens its editor. */
	type Props = {
		items: FridgeItem[];
		liquidUnits: LiquidUnits;
		onEdit: (item: FridgeItem) => void;
	};

	let { items, liquidUnits, onEdit }: Props = $props();

	let groups = $derived(groupByCategory(items));
</script>

<div class="flex flex-col gap-6">
	{#each groups as group (group.category)}
		<section aria-labelledby="cat-{group.category}">
			<h2 id="cat-{group.category}" class="mb-2 font-sans text-base font-semibold text-muted">
				{t.recipes.newProduct.categories[group.category]}
			</h2>
			<ul class="flex flex-col overflow-hidden rounded-card border border-line bg-surface">
				{#each group.items as item (item.id)}
					<li class="border-b border-line last:border-b-0">
						<button
							type="button"
							class="flex min-h-14 w-full items-center gap-3 px-4 py-2 text-left hover:bg-cobalt-wash"
							onclick={() => onEdit(item)}
						>
							<span class="flex min-w-0 flex-1 flex-col">
								<span class="text-base font-semibold text-ink">{item.product.name}</span>
								<span class="tabular text-sm text-muted">
									{inventoryText(item.packageAmount, item.product, liquidUnits)} ·
									{remainingText(item, liquidUnits)}
								</span>
								{#if item.note}
									<span class="truncate text-sm text-muted">{item.note}</span>
								{/if}
							</span>
							<ChevronRight size={20} aria-hidden="true" class="shrink-0 text-muted" />
						</button>
					</li>
				{/each}
			</ul>
		</section>
	{/each}
</div>
