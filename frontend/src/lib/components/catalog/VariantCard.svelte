<script lang="ts">
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import type { components } from '$lib/api/schema';
	import { t } from '$lib/i18n';
	import FavoriteButton from './FavoriteButton.svelte';
	import VariantActions from './VariantActions.svelte';

	/**
	 * A variant in "Ulubione" and "Moje warianty". It opens the base recipe with the variant
	 * preselected (`?wariant=`). When the base recipe is no longer visible, the card says so and offers
	 * only deletion (PROJECT §6.5). The author also gets the ⋯ menu (`VariantActions`).
	 */
	type Props = {
		variant: components['schemas']['VariantSummary'];
		onfavorite?: (favorite: boolean) => void;
		/** The variant was deleted from its menu. */
		ondeleted?: () => void;
	};

	let { variant = $bindable(), onfavorite, ondeleted }: Props = $props();

	let isAuthor = $derived(variant.author.id === page.data.me?.id);

	let href = $derived(
		`${resolve('/przepisy/[id]', { id: variant.recipeId })}?wariant=${variant.id}`
	);
</script>

<article class="relative flex items-start gap-3 rounded-card border border-line bg-surface p-3">
	<div class="flex min-w-0 flex-1 flex-col gap-1">
		<h2 class="text-lg leading-snug">
			{#if variant.baseAvailable}
				<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- resolved above -->
				<a {href} class="after:absolute after:inset-0 after:content-['']">{variant.name}</a>
			{:else}
				{variant.name}
			{/if}
		</h2>
		<p class="text-sm text-muted">
			{t.recipes.catalog.variantOf(variant.name, variant.recipeTitle)}
		</p>
		<p class="text-sm text-muted">{t.recipes.catalog.by(variant.author.nickname)}</p>
		{#if !variant.baseAvailable}
			<p class="text-sm font-semibold text-ink">{t.recipes.catalog.baseUnavailable}</p>
		{/if}
	</div>
	<div class="flex shrink-0 items-center">
		{#if variant.baseAvailable}
			<FavoriteButton
				kind="variant"
				id={variant.id}
				bind:favorite={variant.isFavorite}
				class="relative z-10 shrink-0"
				onchange={onfavorite}
			/>
		{/if}
		{#if isAuthor}
			<VariantActions
				variantId={variant.id}
				name={variant.name}
				visibility={variant.visibility}
				baseAvailable={variant.baseAvailable}
				onvisibility={(v) => (variant.visibility = v)}
				{ondeleted}
			/>
		{/if}
	</div>
</article>
