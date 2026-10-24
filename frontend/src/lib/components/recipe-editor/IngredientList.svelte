<script lang="ts">
	import ArrowDown from '@lucide/svelte/icons/arrow-down';
	import ArrowUp from '@lucide/svelte/icons/arrow-up';
	import CornerDownRight from '@lucide/svelte/icons/corner-down-right';
	import GripVertical from '@lucide/svelte/icons/grip-vertical';
	import Plus from '@lucide/svelte/icons/plus';
	import Trash from '@lucide/svelte/icons/trash';
	import Undo2 from '@lucide/svelte/icons/undo-2';
	import { Button, IconButton } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { emptyIngredient, type DraftIngredient, type EditorMode } from './draft';
	import IngredientRow from './IngredientRow.svelte';
	import { canMove, dropRow, moveRow } from './lists';

	/**
	 * The ingredient rows. Reorder with the arrow buttons (always) or by dragging the grip (desktop).
	 * In `variant` mode base rows cannot be reordered, "remove in variant" marks them removed, and a
	 * new row can be inserted below any row.
	 */
	type Props = {
		ingredients: DraftIngredient[];
		mode?: EditorMode;
		errors?: Record<string, string>;
	};

	let { ingredients = $bindable(), mode = 'recipe', errors = {} }: Props = $props();

	const labels = t.recipes.ingredients;
	let dragFrom = $state<number | null>(null);

	function remove(index: number) {
		const row = ingredients[index];
		if (!row) return;
		if (mode === 'variant' && row.base) row.removed = !row.removed;
		else ingredients = ingredients.filter((_, i) => i !== index);
	}

	function insertAfter(index: number) {
		ingredients = [
			...ingredients.slice(0, index + 1),
			emptyIngredient(),
			...ingredients.slice(index + 1)
		];
	}

	function drop(to: number) {
		if (dragFrom !== null) ingredients = dropRow(ingredients, dragFrom, to);
		dragFrom = null;
	}

	let numbers = $derived(
		ingredients.map((r, i) =>
			r.removed ? 0 : ingredients.slice(0, i + 1).filter((x) => !x.removed).length
		)
	);
</script>

<ul class="flex flex-col gap-4">
	{#each ingredients as ingredient, index (ingredient.key)}
		<li
			role="group"
			aria-label={labels.row(numbers[index] || index + 1)}
			class={[
				'rounded-card border border-line bg-surface p-3',
				ingredient.removed && 'opacity-60',
				dragFrom === index && 'border-cobalt'
			]}
			ondragover={(e) => dragFrom !== null && e.preventDefault()}
			ondrop={(e) => {
				e.preventDefault();
				drop(index);
			}}
		>
			<div class="mb-2 flex items-center gap-1">
				<span
					class="hidden size-11 cursor-grab items-center justify-center text-muted md:inline-flex"
					draggable={mode === 'recipe' || !ingredient.base}
					role="img"
					aria-label={labels.drag}
					title={labels.drag}
					ondragstart={(e) => {
						dragFrom = index;
						e.dataTransfer?.setData('text/plain', String(index));
					}}
					ondragend={() => (dragFrom = null)}
				>
					<GripVertical size={20} aria-hidden="true" />
				</span>
				<h3 class="flex-1 font-sans text-base font-semibold">
					{labels.row(numbers[index] || index + 1)}
				</h3>
				{#if !ingredient.removed}
					<IconButton
						icon={ArrowUp}
						label={labels.moveUp}
						disabled={!canMove(ingredients, index, 'up')}
						onclick={() => (ingredients = moveRow(ingredients, index, 'up'))}
					/>
					<IconButton
						icon={ArrowDown}
						label={labels.moveDown}
						disabled={!canMove(ingredients, index, 'down')}
						onclick={() => (ingredients = moveRow(ingredients, index, 'down'))}
					/>
				{/if}
				{#if ingredient.removed}
					<Button variant="secondary" icon={Undo2} onclick={() => remove(index)}>
						{labels.restore}
					</Button>
				{:else}
					<IconButton
						icon={Trash}
						label={mode === 'variant' && ingredient.base ? labels.removeInVariant : labels.remove}
						onclick={() => remove(index)}
					/>
				{/if}
			</div>
			{#if ingredient.removed}
				<p class="text-base">
					<span class="font-semibold">{ingredient.product?.name}</span>
					<span class="text-muted">· {labels.removedInVariant}</span>
				</p>
			{:else}
				<IngredientRow
					bind:ingredient={ingredients[index]!}
					errors={{
						product: errors[`ingredients[${index}].product`],
						quantity: errors[`ingredients[${index}].quantity`],
						note: errors[`ingredients[${index}].note`]
					}}
				/>
			{/if}
			{#if mode === 'variant'}
				<div class="mt-2">
					<Button variant="ghost" icon={CornerDownRight} onclick={() => insertAfter(index)}>
						{labels.insertBelow}
					</Button>
				</div>
			{/if}
		</li>
	{/each}
</ul>

{#if errors.ingredients}
	<p class="mt-2 text-sm font-semibold text-beet" role="alert">{errors.ingredients}</p>
{/if}

<div class="mt-4">
	<Button
		variant="secondary"
		icon={Plus}
		onclick={() => (ingredients = [...ingredients, emptyIngredient()])}
	>
		{labels.add}
	</Button>
</div>
