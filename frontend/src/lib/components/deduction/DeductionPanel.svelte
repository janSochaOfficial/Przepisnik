<script lang="ts">
	import { onMount } from 'svelte';
	import { SvelteSet } from 'svelte/reactivity';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import { inventoryText } from '$lib/components/fridge/amount';
	import type { DeductionResult, FridgeSummary } from '$lib/components/fridge/types';
	import {
		Button,
		Dialog,
		ErrorMessage,
		Select,
		Skeleton,
		Switch,
		toast
	} from '$lib/components/ui';
	import {
		allAvailable,
		defaultSelection,
		neededFromIngredients,
		planDeduction,
		toDeductionRequest,
		type DeductionRow
	} from '$lib/domain/deduction';
	import { isCookBlocked } from '$lib/domain/cook';
	import { pluralCategory } from '$lib/domain/plural';
	import type { FridgeItem, Ingredient, LiquidUnits } from '$lib/domain/types';
	import { errorMessage, t } from '$lib/i18n';
	import { addRowsToShoppingList } from './addMissing';

	/**
	 * the deduction dialog (PROJECT §9). Opened from the recipe view with the **resolved** ingredients and
	 * the scaling `factor`:
	 *
	 * ```svelte
	 * <DeductionDialog bind:open ingredients={resolved.ingredients} factor={factor}
	 *   recipeId={recipe.id} variantId={variantId} liquidUnits={me.settings.liquidUnits}
	 *   defaultFridgeId={me.settings.defaultFridgeId} />
	 * ```
	 * It loads the fridges and the chosen fridge's items itself while open.
	 * (Internal: mount it through `DeductionDialog`, which creates it only while open.)
	 */
	type Props = {
		open?: boolean;
		/** The currently resolved ingredients (variant picks applied). "Do smaku" rows are ignored. */
		ingredients: Ingredient[];
		/** The scaling factor of the recipe view (1 = as written). */
		factor?: number;
		recipeId?: string | null;
		variantId?: string | null;
		/** The caller's last counted cook of the recipe (`RecipeDetail.myLastCountedCookAt`, PROJECT §9.1). */
		lastCountedCookAt?: string | null;
		defaultFridgeId?: string | null;
		liquidUnits: LiquidUnits;
		onDeducted?: (result: DeductionResult) => void;
	};

	let {
		open = $bindable(false),
		ingredients,
		factor = 1,
		recipeId = null,
		variantId = null,
		lastCountedCookAt = null,
		defaultFridgeId = null,
		liquidUnits,
		onDeducted
	}: Props = $props();

	const labels = t.fridge.deduction;

	let fridges = $state<FridgeSummary[]>([]);
	let fridgeId = $state('');
	let items = $state<FridgeItem[]>([]);
	let loading = $state(false);
	let loadError = $state<unknown>(null);
	let actionError = $state<unknown>(null);
	let busy = $state<'selected' | 'all' | 'list' | null>(null);
	const selection = new SvelteSet<string>();

	let rows = $derived(planDeduction(neededFromIngredients(ingredients, factor), items));
	// The deduction always happens; within 12 hours of a counted cook it only does not raise the count.
	let notCounted = $derived(recipeId !== null && isCookBlocked(lastCountedCookAt, new Date()));
	let fridgeItems = $derived(fridges.map((f) => ({ value: f.id, label: f.name })));

	let requestId = 0;
	async function loadItems(id: string) {
		const current = ++requestId;
		loading = true;
		loadError = null;
		try {
			const list = await unwrap(
				createApi().GET('/fridges/{id}/items', { params: { path: { id } } })
			);
			if (current !== requestId) return;
			items = list;
			selection.clear();
			for (const key of defaultSelection(
				planDeduction(neededFromIngredients(ingredients, factor), list)
			)) {
				selection.add(key);
			}
		} catch (e) {
			if (current === requestId) loadError = e;
		} finally {
			if (current === requestId) loading = false;
		}
	}

	async function init() {
		loading = true;
		loadError = null;
		actionError = null;
		try {
			const list = await unwrap(createApi().GET('/fridges'));
			fridges = list;
			fridgeId = (list.find((f) => f.id === defaultFridgeId) ?? list[0])?.id ?? '';
			if (fridgeId) await loadItems(fridgeId);
			else loading = false;
		} catch (e) {
			loadError = e;
			loading = false;
		}
	}

	onMount(init);

	function toggle(row: DeductionRow, on: boolean) {
		if (on) selection.add(row.product.id);
		else selection.delete(row.product.id);
	}

	function describe(row: DeductionRow): string {
		const needed = labels.needed(inventoryText(row.needed, row.product, liquidUnits));
		if (row.status === 'missing') return `${needed} · ${labels.missing}`;
		if (row.status === 'short') {
			return `${needed} · ${labels.short(inventoryText(row.available, row.product, liquidUnits))}`;
		}
		return `${needed} · ${labels.available(inventoryText(row.available, row.product, liquidUnits))}`;
	}

	async function deduct(chosen: ReadonlySet<string>, kind: 'selected' | 'all') {
		const request = toDeductionRequest(rows, chosen, { recipeId, variantId });
		if (request.lines.length === 0) {
			actionError = new ApiError(400, 'deduction.empty');
			return;
		}
		busy = kind;
		actionError = null;
		try {
			const result = await unwrap(
				createApi().POST('/fridges/{id}/deductions', {
					params: { path: { id: fridgeId } },
					body: request
				})
			);
			open = false;
			const count = result.lines.filter((l) => l.deducted > 0).length;
			toast.show(labels.deducted(count, labels.products[pluralCategory(count)]), {
				action: { label: t.common.actions.undo, onAction: () => undo(result.id) }
			});
			onDeducted?.(result);
		} catch (e) {
			actionError = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			busy = null;
		}
	}

	/** The undo is offered only for the deduction that was just made (its own `id`). */
	async function undo(deductionId: string) {
		const id = fridgeId;
		try {
			await unwrap(
				createApi().POST('/fridges/{id}/deductions/{deductionId}/undo', {
					params: { path: { id, deductionId } }
				})
			);
			toast.success(labels.undone);
		} catch (e) {
			toast.error(e instanceof ApiError ? errorMessage(e.code) : labels.undoFailed, {
				duration: 10000
			});
		}
	}

	async function addMissing() {
		busy = 'list';
		actionError = null;
		try {
			await addRowsToShoppingList(fridgeId, rows);
		} catch (e) {
			actionError = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			busy = null;
		}
	}

	let canDeduct = $derived(!loading && rows.some((r) => r.status !== 'missing'));
	let hasMissing = $derived(rows.some((r) => r.status !== 'ok'));
</script>

<Dialog bind:open title={labels.title} description={labels.description} size="lg">
	<div class="flex flex-col gap-5">
		<ErrorMessage error={actionError} />
		{#if notCounted}
			<p role="status" class="rounded-card border border-line-strong bg-cobalt-wash p-3 text-base">
				{labels.cookNotCounted}
			</p>
		{/if}
		{#if loadError}
			<ErrorMessage error={loadError} variant="block" retry={init} />
		{:else}
			<Select
				label={labels.fridgeLabel}
				items={fridgeItems}
				bind:value={fridgeId}
				onValueChange={(id) => void loadItems(id)}
			/>
			{#if loading}
				<div class="flex flex-col gap-3" aria-busy="true">
					<Skeleton class="h-12 w-full" />
					<Skeleton class="h-12 w-full" />
					<Skeleton class="h-12 w-full" />
				</div>
			{:else if rows.length === 0}
				<p class="text-base text-muted">{labels.noRows}</p>
			{:else}
				<ul aria-label={labels.rows} class="flex flex-col divide-y divide-line">
					{#each rows as row (row.product.id)}
						<li class="py-1">
							<Switch
								label={row.product.name}
								description={describe(row)}
								checked={selection.has(row.product.id)}
								disabled={row.status === 'missing'}
								onCheckedChange={(on) => toggle(row, on)}
							/>
						</li>
					{/each}
				</ul>
			{/if}
		{/if}
	</div>
	{#snippet footer()}
		{#if hasMissing}
			<Button
				variant="ghost"
				loading={busy === 'list'}
				disabled={loading || !fridgeId}
				onclick={addMissing}
			>
				{labels.addMissing}
			</Button>
		{/if}
		<Button
			variant="secondary"
			loading={busy === 'all'}
			disabled={!canDeduct}
			onclick={() => deduct(allAvailable(rows), 'all')}
		>
			{labels.deductAll}
		</Button>
		<Button
			loading={busy === 'selected'}
			disabled={!canDeduct || selection.size === 0}
			onclick={() => deduct(selection, 'selected')}
		>
			{labels.deductSelected}
		</Button>
	{/snippet}
</Dialog>
