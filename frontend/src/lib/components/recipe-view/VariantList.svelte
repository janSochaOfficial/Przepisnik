<script lang="ts">
	import { page } from '$app/state';
	import { createApi, unwrap } from '$lib/api/client';
	import FavoriteButton from '$lib/components/catalog/FavoriteButton.svelte';
	import VariantActions from '$lib/components/catalog/VariantActions.svelte';
	import { Button, EmptyState } from '$lib/components/ui';
	import type { Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import type { SelectionState } from '$lib/state/selection.svelte';

	/**
	 * The Warianty tab (PROJECT §6.7): "Przepis bazowy" first, then the visible variants, most cooked
	 * first, then oldest first. Opening an entry reports its id ('base' for the base recipe).
	 */
	type Props = {
		state: SelectionState;
		/** The variant shown now; null for a custom mix. */
		shownId: string | null;
		onopen: (id: string) => void;
		oncreate?: () => void;
		/** The viewer deleted one of their variants from its menu. */
		ondeleted?: (id: string) => void;
	};

	let { state: selection, shownId, onopen, oncreate, ondeleted }: Props = $props();

	let meId = $derived(page.data.me?.id);

	const labels = t.variants.list;

	let base = $derived(selection.matrix.variants.find((v) => v.id === 'base'));
	let variants = $derived(
		selection.matrix.variants
			.filter((v) => v.id !== 'base')
			.toSorted(
				(a, b) =>
					b.cookCount - a.cookCount ||
					new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
			)
	);

	// The matrix has no visibility; it is filled in when the summaries arrive.
	let visibility = $state.raw<Record<string, Visibility>>({});
	$effect(() => {
		const id = selection.recipe.id;
		let stale = false;
		unwrap(createApi().GET('/recipes/{id}/variants', { params: { path: { id } } }))
			.then((list) => {
				if (!stale) visibility = Object.fromEntries(list.map((v) => [v.id, v.visibility]));
			})
			.catch(() => {});
		return () => (stale = true);
	});
</script>

{#if variants.length === 0}
	<EmptyState title={labels.emptyTitle} description={labels.emptyText}>
		{#snippet action()}
			{#if oncreate}<Button onclick={oncreate}>{labels.create}</Button>{/if}
		{/snippet}
	</EmptyState>
{:else}
	<ul class="flex flex-col gap-2" aria-label={labels.label}>
		<li class="flex items-center gap-2 rounded-card border border-line bg-surface p-3">
			<button
				type="button"
				class="flex min-w-0 flex-1 flex-col gap-1 text-left"
				aria-current={shownId === 'base' ? 'true' : undefined}
				onclick={() => onopen('base')}
			>
				<span class="text-lg leading-snug font-semibold">{labels.base}</span>
				<span class="text-sm text-muted">
					{labels.by(selection.recipe.author.nickname)} · {labels.cooked(base?.cookCount ?? 0)}
				</span>
			</button>
			{#if shownId === 'base'}<span class="text-sm font-semibold text-cobalt">{labels.shown}</span
				>{/if}
		</li>
		{#each variants as v (v.id)}
			<li class="flex items-center gap-2 rounded-card border border-line bg-surface p-3">
				<button
					type="button"
					class="flex min-w-0 flex-1 flex-col gap-1 text-left"
					aria-current={shownId === v.id ? 'true' : undefined}
					onclick={() => onopen(v.id)}
				>
					<span class="text-lg leading-snug font-semibold">{v.name}</span>
					<span class="text-sm text-muted">
						{labels.by(v.author.nickname)}
						{#if visibility[v.id]}· {t.recipes.visibility[visibility[v.id]!]}{/if}
						· {labels.cooked(v.cookCount)}
					</span>
				</button>
				{#if shownId === v.id}<span class="text-sm font-semibold text-cobalt">{labels.shown}</span
					>{/if}
				<FavoriteButton
					kind="variant"
					id={v.id}
					favorite={v.isFavorite}
					addLabel={labels.favoriteAdd(v.name)}
					removeLabel={labels.favoriteRemove(v.name)}
					onchange={(on) => selection.setFavorite(v.id, on)}
				/>
				{#if v.author.id === meId}
					<VariantActions
						variantId={v.id}
						name={v.name}
						visibility={visibility[v.id]}
						onvisibility={(next) => (visibility = { ...visibility, [v.id]: next })}
						ondeleted={() => ondeleted?.(v.id)}
					/>
				{/if}
			</li>
		{/each}
	</ul>
{/if}
