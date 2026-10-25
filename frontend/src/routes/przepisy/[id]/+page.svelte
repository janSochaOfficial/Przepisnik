<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import Ellipsis from '@lucide/svelte/icons/ellipsis';
	import Globe from '@lucide/svelte/icons/globe';
	import Lock from '@lucide/svelte/icons/lock';
	import Refrigerator from '@lucide/svelte/icons/refrigerator';
	import { onDestroy } from 'svelte';
	import ChangeVisibilityDialog from '$lib/components/catalog/ChangeVisibilityDialog.svelte';
	import FavoriteButton from '$lib/components/catalog/FavoriteButton.svelte';
	import DeleteRecipeDialog from '$lib/components/recipe-editor/DeleteRecipeDialog.svelte';
	import { exportRecipe } from '$lib/components/recipe-editor/transfer';
	import { setCookContext } from '$lib/components/recipe-view/cook-context';
	import CookActions from '$lib/components/recipe-view/CookActions.svelte';
	import IngredientRow from '$lib/components/recipe-view/IngredientRow.svelte';
	import ServingsControl from '$lib/components/recipe-view/ServingsControl.svelte';
	import StepList from '$lib/components/recipe-view/StepList.svelte';
	import { draftFromSelection } from '$lib/components/recipe-view/variant-draft';
	import { Page } from '$lib/components/shell';
	import VariantList from '$lib/components/recipe-view/VariantList.svelte';
	import {
		BottomSheet,
		Button,
		Chip,
		ErrorMessage,
		Skeleton,
		Tabs,
		toast
	} from '$lib/components/ui';
	import { buttonClass, iconButtonClass } from '$lib/components/ui/variants';
	import { formatBaseAmount, formatNumberPl } from '$lib/domain/format';
	import { formatServings } from '$lib/domain/scaling';
	import { t } from '$lib/i18n';
	import { SelectionState } from '$lib/state/selection.svelte';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	// The state is seeded once from the loaded data; from then on the user's picks are the truth.
	// svelte-ignore state_referenced_locally
	const selection = new SelectionState({
		recipe: data.recipe,
		matrix: data.matrix,
		selection: data.selection,
		preferences: data.preferences,
		liquidUnits: data.me?.settings.liquidUnits ?? 'kitchen',
		pinVariantId: data.pinVariantId
	});

	setCookContext({
		get recipeId() {
			return selection.recipe.id;
		},
		get variantId() {
			return selection.match.bestVariantId;
		},
		get ingredients() {
			return selection.cookIngredients;
		},
		get lastCountedCookAt() {
			return selection.recipe.myLastCountedCookAt ?? null;
		},
		countCook: (variantId, countedAt) => selection.countCook(variantId, countedAt)
	});

	$effect(() => {
		void selection.ensureResolved(selection.match.bestVariantId);
	});
	onDestroy(() => selection.flush());

	// The selected tab is not in the URL (PROJECT §6.7).
	let tab = $state('recipe');

	// `?wariant=` is already applied by the constructor; a later change (a link, back / forward)
	// re-applies it without a reload. Unknown ids leave the remembered state alone.
	// svelte-ignore state_referenced_locally
	let appliedPin = data.pinVariantId;
	$effect(() => {
		const id = data.pinVariantId;
		if (id === appliedPin) return;
		appliedPin = id;
		if (id && selection.openVariant(id)) tab = 'recipe';
	});

	/** An entry of the Warianty tab: shows exactly that variant, or the base (no `?wariant=`). */
	function openVariant(id: string) {
		if (!selection.openVariant(id)) return;
		tab = 'recipe';
		const target = resolve('/przepisy/[id]', { id: selection.recipe.id });
		appliedPin = id === 'base' ? null : id;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- resolved above
		void goto(id === 'base' ? target : `${target}?wariant=${id}`, {
			noScroll: true,
			keepFocus: true
		});
	}

	/** The author deleted a variant from the Warianty tab: reload the matrix; leave it if it was shown. */
	async function variantDeleted(id: string) {
		const wasShown = selection.match.bestVariantId === id || data.pinVariantId === id;
		try {
			await selection.reloadMatrix();
		} catch {
			toast.error(t.errors['server.error']);
			return;
		}
		if (!wasShown) return;
		selection.openVariant('base');
		appliedPin = null;
		void goto(resolve('/przepisy/[id]', { id: selection.recipe.id }), {
			noScroll: true,
			keepFocus: true
		});
	}

	const labels = t.variants;
	let recipe = $derived(selection.recipe);
	let isAuthor = $derived(recipe.author.id === data.me?.id);
	let active = $derived(selection.stepsSource);
	let match = $derived(selection.match);
	let variantFavorite = $derived(selection.bestVariant?.isFavorite ?? false);
	let activeName = $derived(
		match.isCustomMix
			? labels.view.customMix
			: match.bestVariantId === 'base'
				? labels.view.baseName
				: (selection.bestVariant?.name ?? '')
	);
	let canCreateVariant = $derived(!recipe.isArchived);
	let publicByAuthor = $derived(isAuthor && recipe.visibility === 'public' && !recipe.isArchived);

	const visibilityIcon = { private: Lock, fridges: Refrigerator, public: Globe } as const;
	let VisibilityIcon = $derived(visibilityIcon[recipe.visibility]);

	/** What the scaling banner names: the servings choice or the "Mam: …" ingredient. */
	let anchor = $derived.by(() => {
		const a = selection.scaling;
		if (a?.kind === 'servings') return formatServings(a.servings);
		const ingredient = a && selection.rows.find((r) => r.slotKey === a.anchorSlotKey)?.ingredient;
		if (!a || !ingredient) return null;
		const amount = formatBaseAmount(a.anchorBaseAmount, ingredient.product, {
			liquidUnits: selection.liquidUnits
		});
		return `${ingredient.product.name} ${amount.text}`;
	});

	let tabItems = $derived([
		{ value: 'recipe', label: labels.tabs.recipe },
		{
			value: 'variants',
			label: labels.tabs.variants(selection.matrix.variants.filter((v) => v.id !== 'base').length)
		}
	]);

	let menuOpen = $state(false);
	let visibilityOpen = $state(false);
	let deleting = $state(false);

	function newVariant(seeded: boolean) {
		const state = seeded
			? { variantDraft: draftFromSelection(recipe, active, selection.rows) }
			: {};
		void goto(resolve('/przepisy/[id]/warianty/nowy', { id: recipe.id }), { state });
	}

	async function exportIt() {
		menuOpen = false;
		try {
			await exportRecipe(recipe.id);
		} catch {
			toast.error(t.recipes.transfer.exportFailed);
		}
	}
</script>

<Page title={recipe.title} back={resolve('/przepisy')} width="wide">
	{#snippet titleAside()}
		<FavoriteButton
			kind="recipe"
			id={recipe.id}
			favorite={recipe.isFavorite}
			addLabel={labels.favorite.recipeAdd}
			removeLabel={labels.favorite.recipeRemove}
			onchange={(on) => selection.setFavorite('base', on)}
		/>
	{/snippet}
	{#snippet actions()}
		<BottomSheet bind:open={menuOpen} title={labels.menu.title}>
			{#snippet trigger({ props })}
				<button {...props} type="button" class={iconButtonClass('ghost', 'md')}>
					<span class="sr-only">{labels.menu.open}</span>
					<Ellipsis size={22} aria-hidden="true" />
				</button>
			{/snippet}
			<ul class="flex flex-col">
				{#if recipe.canEdit}
					<li>
						<a
							class={buttonClass('ghost', 'md') + ' w-full justify-start'}
							href={resolve('/przepisy/[id]/edytuj', { id: recipe.id })}>{labels.menu.edit}</a
						>
					</li>
				{/if}
				{#if canCreateVariant}
					<li>
						<button
							type="button"
							class={buttonClass('ghost', 'md') + ' w-full justify-start'}
							onclick={() => {
								menuOpen = false;
								newVariant(false);
							}}
						>
							{publicByAuthor ? labels.menu.createVariant : labels.menu.newVariant}
						</button>
					</li>
				{/if}
				{#if isAuthor && !recipe.isArchived}
					<li>
						<button
							type="button"
							class={buttonClass('ghost', 'md') + ' w-full justify-start'}
							onclick={() => {
								menuOpen = false;
								visibilityOpen = true;
							}}>{labels.menu.changeVisibility}</button
						>
					</li>
				{/if}
				<li>
					<button
						type="button"
						class={buttonClass('ghost', 'md') + ' w-full justify-start'}
						onclick={exportIt}>{labels.menu.export}</button
					>
				</li>
				{#if isAuthor}
					<li>
						<button
							type="button"
							class={buttonClass('ghost', 'md') + ' w-full justify-start text-beet'}
							onclick={() => {
								menuOpen = false;
								deleting = true;
							}}>{labels.menu.delete}</button
						>
					</li>
				{/if}
			</ul>
		</BottomSheet>
	{/snippet}

	<header class="flex flex-col gap-3">
		{#if recipe.coverImage}
			<img
				src={recipe.coverImage.url}
				alt={labels.view.coverAlt}
				width={recipe.coverImage.width}
				height={recipe.coverImage.height}
				class="max-h-96 w-full rounded-card border border-line object-cover"
			/>
		{/if}
		<div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-muted">
			<span class="inline-flex items-center gap-1">
				<VisibilityIcon size={16} aria-hidden="true" />
				<span class="sr-only">{labels.view.visibilityIcon}:</span>
				{t.recipes.visibility.iconLabel[recipe.visibility]}
			</span>
			<span>{labels.view.by(recipe.author.nickname)}</span>
			{#if active.prepMinutes !== null}<span>{labels.view.prepTime(active.prepMinutes)}</span>{/if}
			{#if active.cookMinutes !== null}<span>{labels.view.cookTime(active.cookMinutes)}</span>{/if}
		</div>

		{#if recipe.tags.length > 0 || active.utensils.length > 0}
			<div class="flex flex-wrap gap-2">
				{#each recipe.tags as tag (tag.id)}<Chip>{tag.name}</Chip>{/each}
				{#each active.utensils as u (u.id)}<Chip>{u.name}</Chip>{/each}
			</div>
		{/if}
		{#if recipe.description}<p>{recipe.description}</p>{/if}
	</header>

	<Tabs class="mt-6" items={tabItems} bind:value={tab} label={labels.tabs.label}>
		{#snippet panel(value)}
			{#if value === 'variants'}
				<VariantList
					state={selection}
					shownId={match.isCustomMix ? null : match.bestVariantId}
					onopen={openVariant}
					ondeleted={variantDeleted}
					oncreate={canCreateVariant ? () => newVariant(false) : undefined}
				/>
			{:else}
				<div class="flex flex-wrap items-center gap-2">
					<p
						class="flex items-center gap-1 text-lg font-semibold"
						aria-label={labels.view.activeVariant}
					>
						{activeName}
					</p>
					{#if !match.isCustomMix && match.bestVariantId !== 'base'}
						{#key match.bestVariantId}
							<FavoriteButton
								kind="variant"
								id={match.bestVariantId}
								favorite={variantFavorite}
								addLabel={labels.favorite.variantAdd}
								removeLabel={labels.favorite.variantRemove}
								onchange={(on) => selection.setFavorite(match.bestVariantId, on)}
							/>
						{/key}
					{/if}
				</div>
				{#if match.isCustomMix}
					<p class="text-sm text-muted">{labels.view.customMixHint}</p>
				{/if}

				<div
					class="mt-6 flex flex-col gap-8 md:grid md:grid-cols-[minmax(0,2fr)_minmax(0,3fr)] md:gap-10"
				>
					<section aria-labelledby="ingredients-h">
						<h2 id="ingredients-h" class="text-2xl">{labels.view.ingredients}</h2>

						<ServingsControl state={selection} />

						{#if selection.scaling && selection.factor !== 1 && anchor}
							<div
								role="status"
								class="mt-3 flex flex-wrap items-center gap-2 rounded-card border border-line-strong bg-cobalt-wash p-3 text-base"
							>
								<span class="flex-1"
									>{labels.scaling.banner(formatNumberPl(selection.factor, 2), anchor)}</span
								>
								<Button variant="secondary" onclick={() => selection.resetScaling()}>
									{labels.scaling.reset}
								</Button>
							</div>
						{/if}

						<ul class="mt-3" aria-label={labels.view.ingredientList}>
							{#each selection.rows.filter((r) => r.ingredient || r.isBase) as row (row.slotKey)}
								<IngredientRow
									{row}
									state={selection}
									{canCreateVariant}
									oncreate={() => newVariant(true)}
								/>
							{/each}
						</ul>

						{#if selection.hasUserPins}
							<div class="mt-3">
								<Button variant="ghost" onclick={() => selection.clearPins()}>
									{labels.clearSelection}
								</Button>
							</div>
						{/if}
					</section>

					<div class="flex flex-col gap-8" aria-busy={selection.stepsLoading}>
						<ErrorMessage error={selection.stepsError} variant="inline" />
						{#if selection.stepsLoading}
							<Skeleton shape="block" class="h-24 w-full" />
						{/if}
						{#if active.prepSteps.length > 0}
							<section aria-labelledby="prep-h">
								<h2 id="prep-h" class="mb-3 text-2xl">{labels.view.prep}</h2>
								<StepList steps={active.prepSteps} label={labels.view.prep} />
							</section>
						{/if}
						<section aria-labelledby="instr-h">
							<h2 id="instr-h" class="mb-3 text-2xl">{labels.view.instructions}</h2>
							<StepList steps={active.instructionSteps} label={labels.view.instructions} />
						</section>
					</div>
				</div>

				<CookActions />
			{/if}
		{/snippet}
	</Tabs>
</Page>

{#if isAuthor && !recipe.isArchived}
	<ChangeVisibilityDialog
		bind:open={visibilityOpen}
		id={recipe.id}
		visibility={recipe.visibility}
		onChanged={(updated) => (selection.recipe = updated)}
	/>
{/if}
{#if isAuthor}
	<DeleteRecipeDialog
		bind:open={deleting}
		recipeId={recipe.id}
		foreignVariantCount={recipe.foreignVariantCount}
		onDeleted={() => void goto(resolve('/przepisy'))}
	/>
{/if}
