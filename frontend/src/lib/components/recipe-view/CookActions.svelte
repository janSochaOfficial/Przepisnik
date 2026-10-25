<script lang="ts">
	import { page } from '$app/state';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import { addMissingToShoppingList } from '$lib/components/deduction/addMissing';
	import DeductionDialog from '$lib/components/deduction/DeductionDialog.svelte';
	import { defaultFridgeIdOf, liquidUnitsOf } from '$lib/components/fridge/amount';
	import { ActionBar } from '$lib/components/shell';
	import { Button, toast } from '$lib/components/ui';
	import {
		cookBlockedUntil,
		COOK_WINDOW_MS,
		formatRetryTime,
		isCookBlocked
	} from '$lib/domain/cook';
	import { errorMessage, t } from '$lib/i18n';
	import { getCookContext } from './cook-context';

	// The sticky bottom bar of the recipe view: "Ugotowane!", the deduction dialog and "add missing to
	// the list" (F6, C1). They read the shown variant and the current, already scaled ingredients from
	// the cook context.
	const cook = getCookContext();
	const labels = t.variants.actions;

	let open = $state(false);
	let adding = $state(false);
	let cooking = $state(false);

	// The 12-hour rule (PROJECT §9.1): the client clock against the server's timestamp; the server
	// still answers 409. `knownLast` keeps what a 409 told us until the recipe data catches up.
	let knownLast = $state<string | null>(null);
	let now = $state(new Date());
	let lastCounted = $derived.by(() => {
		const fromRecipe = cook.lastCountedCookAt;
		if (!knownLast) return fromRecipe;
		if (!fromRecipe) return knownLast;
		return Date.parse(knownLast) > Date.parse(fromRecipe) ? knownLast : fromRecipe;
	});
	let blocked = $derived(isCookBlocked(lastCounted, now));
	let until = $derived(cookBlockedUntil(lastCounted));
	let hint = $derived(blocked && until ? labels.cookedHint(formatRetryTime(until, now)) : null);

	// Re-enable the button when the 12 hours pass while the page stays open.
	$effect(() => {
		if (!blocked || !until) return;
		const timer = setTimeout(() => (now = new Date()), until.getTime() - now.getTime() + 50);
		return () => clearTimeout(timer);
	});

	// The context amounts are already multiplied by the scaling factor, so the factor here is 1.
	let ingredients = $derived(
		cook.ingredients.map((i) => ({ ...i, baseAmount: i.scaledBaseAmount }))
	);
	let variantId = $derived(cook.variantId === 'base' ? null : cook.variantId);

	function countedAtOf(nextAllowedAt: unknown): string | null {
		const next = typeof nextAllowedAt === 'string' ? Date.parse(nextAllowedAt) : NaN;
		return Number.isNaN(next) ? null : new Date(next - COOK_WINDOW_MS).toISOString();
	}

	async function cooked() {
		cooking = true;
		const shown = cook.variantId;
		try {
			const result = await unwrap(
				createApi().POST('/recipes/{id}/cooked', {
					params: { path: { id: cook.recipeId } },
					body: { variantId: shown === 'base' ? null : shown }
				})
			);
			const countedAt = countedAtOf(result.nextAllowedAt) ?? new Date().toISOString();
			knownLast = countedAt;
			now = new Date();
			cook.countCook(shown, countedAt);
			toast.success(labels.cookedDone);
		} catch (e) {
			if (e instanceof ApiError && e.code === 'cook.too_soon') {
				// Another tab or a deduction counted it already: show the same hint.
				knownLast = countedAtOf(e.problem?.nextAllowedAt) ?? new Date().toISOString();
				now = new Date();
			} else {
				toast.error(e instanceof ApiError ? errorMessage(e.code) : errorMessage('server.error'));
			}
		} finally {
			cooking = false;
		}
	}

	/** A deduction the server counted as a cook (`cookCounted`) starts the 12 hours too. */
	function deducted(result: { cookCounted: boolean; createdAt: string }) {
		if (!result.cookCounted) return;
		const countedAt = result.createdAt;
		knownLast = countedAt;
		now = new Date();
		cook.countCook(cook.variantId, countedAt);
	}

	async function addMissing() {
		adding = true;
		try {
			await addMissingToShoppingList({
				ingredients,
				factor: 1,
				fridgeId: defaultFridgeIdOf(page.data)
			});
		} finally {
			adding = false;
		}
	}
</script>

{#if hint}
	<p id="cooked-hint" class="mt-6 text-sm text-muted" role="status">{hint}</p>
{/if}
<ActionBar label={labels.group}>
	<Button variant="secondary" loading={adding} onclick={addMissing}>
		{labels.addMissing}
	</Button>
	<Button
		variant="secondary"
		loading={cooking}
		disabled={blocked}
		aria-describedby={hint ? 'cooked-hint' : undefined}
		onclick={cooked}
	>
		{labels.cooked}
	</Button>
	<Button onclick={() => (open = true)}>{labels.deduct}</Button>
</ActionBar>

<DeductionDialog
	bind:open
	{ingredients}
	factor={1}
	recipeId={cook.recipeId}
	{variantId}
	lastCountedCookAt={lastCounted}
	onDeducted={deducted}
	defaultFridgeId={defaultFridgeIdOf(page.data)}
	liquidUnits={liquidUnitsOf(page.data)}
/>
