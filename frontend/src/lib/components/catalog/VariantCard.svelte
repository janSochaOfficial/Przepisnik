<script lang="ts">
	import Trash from '@lucide/svelte/icons/trash';
	import { resolve } from '$app/paths';
	import type { components } from '$lib/api/schema';
	import { IconButton } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import FavoriteButton from './FavoriteButton.svelte';

	/**
	 * A variant in "Ulubione" and "Moje warianty". It opens the base recipe with the variant
	 * preselected (`?wariant=`). When the base recipe is no longer visible, the card says so and offers
	 * only deletion (PROJECT §6.5).
	 */
	type Props = {
		variant: components['schemas']['VariantSummary'];
		onfavorite?: (favorite: boolean) => void;
		ondelete?: () => void;
	};

	let { variant = $bindable(), onfavorite, ondelete }: Props = $props();

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
	{#if variant.baseAvailable}
		<FavoriteButton
			kind="variant"
			id={variant.id}
			bind:favorite={variant.isFavorite}
			class="relative z-10 shrink-0"
			onchange={onfavorite}
		/>
	{:else if ondelete}
		<IconButton
			icon={Trash}
			label={t.recipes.catalog.deleteVariant}
			class="relative z-10 shrink-0"
			onclick={ondelete}
		/>
	{/if}
</article>
