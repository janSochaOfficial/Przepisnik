<script lang="ts">
	import Pencil from '@lucide/svelte/icons/pencil';
	import { inventoryText } from '$lib/components/fridge/amount';
	import { IconButton } from '$lib/components/ui';
	import type { LiquidUnits, ShoppingListItem } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	/** One entry of the list: a big check target with the name, the amount, and an edit button. */
	type Props = {
		item: ShoppingListItem;
		liquidUnits: LiquidUnits;
		onToggle: (item: ShoppingListItem) => void;
		onEdit: (item: ShoppingListItem) => void;
	};

	let { item, liquidUnits, onToggle, onEdit }: Props = $props();

	let name = $derived(item.product?.name ?? item.text ?? '');
	let amount = $derived(
		item.product && item.amount !== null && item.amount > 0
			? inventoryText(item.amount, item.product, liquidUnits)
			: null
	);
</script>

<li class="flex items-center border-b border-line last:border-b-0">
	<label class="flex min-h-14 flex-1 cursor-pointer items-center gap-3 py-2 pl-4">
		<input
			type="checkbox"
			checked={item.checked}
			onchange={() => onToggle(item)}
			aria-label={t.shopping.check(name)}
			class="size-6 shrink-0 cursor-pointer accent-cobalt"
		/>
		<span class={['flex min-w-0 flex-1 flex-col', item.checked && 'text-muted line-through']}>
			<span class="text-base font-semibold">{name}</span>
			{#if amount}
				<span class="tabular text-sm">{amount}</span>
			{/if}
		</span>
	</label>
	<IconButton icon={Pencil} label={t.shopping.editItem(name)} onclick={() => onEdit(item)} />
</li>
