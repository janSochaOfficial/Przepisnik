<script lang="ts">
	import Globe from '@lucide/svelte/icons/globe';
	import ImageOff from '@lucide/svelte/icons/image-off';
	import Lock from '@lucide/svelte/icons/lock';
	import Refrigerator from '@lucide/svelte/icons/refrigerator';
	import { resolve } from '$app/paths';
	import type { components } from '$lib/api/schema';
	import { Chip } from '$lib/components/ui';
	import { pluralCategory } from '$lib/domain/plural';
	import { t } from '$lib/i18n';
	import FavoriteButton from './FavoriteButton.svelte';

	/** A recipe in the catalog: cover thumbnail, title, author, times, tags, variants, visibility and ♥. */
	type Props = {
		recipe: components['schemas']['RecipeSummary'];
		onfavorite?: (favorite: boolean) => void;
	};

	let { recipe = $bindable(), onfavorite }: Props = $props();

	const icons = { private: Lock, fridges: Refrigerator, public: Globe } as const;
	let VisibilityIcon = $derived(icons[recipe.visibility]);
	let times = $derived(
		[
			recipe.prepMinutes ? t.recipes.catalog.prepTime(recipe.prepMinutes) : null,
			recipe.cookMinutes ? t.recipes.catalog.cookTime(recipe.cookMinutes) : null
		].filter((x) => x !== null)
	);
</script>

<article class="relative flex gap-3 rounded-card border border-line bg-surface p-3">
	<div
		class="flex size-24 shrink-0 items-center justify-center overflow-hidden rounded-control bg-sunken text-muted"
	>
		{#if recipe.coverImage}
			<img
				src={recipe.coverImage.thumbnailUrl}
				alt=""
				loading="lazy"
				class="size-full object-cover"
			/>
		{:else}
			<ImageOff size={28} aria-label={t.recipes.catalog.noCover} role="img" />
		{/if}
	</div>
	<div class="flex min-w-0 flex-1 flex-col gap-1">
		<h2 class="text-lg leading-snug">
			<a
				href={resolve('/przepisy/[id]', { id: recipe.id })}
				class="after:absolute after:inset-0 after:content-['']"
			>
				{recipe.title}
			</a>
		</h2>
		<p class="text-sm text-muted">
			{[t.recipes.catalog.by(recipe.author.nickname), ...times].join(' · ')}
		</p>
		{#if recipe.tags.length > 0}
			<ul class="flex flex-wrap gap-1.5" aria-label={t.recipes.editor.tags}>
				{#each recipe.tags.slice(0, 4) as tag (tag.id)}
					<li><Chip>{tag.name}</Chip></li>
				{/each}
			</ul>
		{/if}
		<p class="flex items-center gap-3 text-sm text-muted">
			<VisibilityIcon
				size={18}
				role="img"
				aria-label={t.recipes.visibility.iconLabel[recipe.visibility]}
			/>
			{#if recipe.variantCount > 0}
				<span>
					{t.recipes.catalog.variantCount[pluralCategory(recipe.variantCount)](recipe.variantCount)}
				</span>
			{/if}
		</p>
	</div>
	<FavoriteButton
		kind="recipe"
		id={recipe.id}
		bind:favorite={recipe.isFavorite}
		class="relative z-10 shrink-0"
		onchange={onfavorite}
	/>
</article>
