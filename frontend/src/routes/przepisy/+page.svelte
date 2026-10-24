<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import SearchX from '@lucide/svelte/icons/search-x';
	import Upload from '@lucide/svelte/icons/upload';
	import { onMount } from 'svelte';
	import { SvelteURLSearchParams } from 'svelte/reactivity';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { createApi, unwrap } from '$lib/api/client';
	import { setFavorite } from '$lib/components/catalog/favorites';
	import { loadNames, type Named } from '$lib/components/catalog/names';
	import RecipeCard from '$lib/components/catalog/RecipeCard.svelte';
	import {
		CatalogList,
		isFilterable,
		type CatalogEntry,
		type CatalogSub,
		type CatalogTab,
		type CatalogView
	} from '$lib/components/catalog/catalog.svelte';
	import VariantCard from '$lib/components/catalog/VariantCard.svelte';
	import ImportDialog from '$lib/components/recipe-editor/ImportDialog.svelte';
	import { Page } from '$lib/components/shell';
	import {
		Button,
		Chip,
		ConfirmDialog,
		EmptyState,
		ErrorMessage,
		IconButton,
		Skeleton,
		Tabs,
		TextField,
		toast
	} from '$lib/components/ui';
	import { t } from '$lib/i18n';

	const labels = t.recipes.catalog;

	const params = page.url.searchParams;
	const asTab = (v: string | null): CatalogTab => (v === 'mine' || v === 'variants' ? v : 'all');
	let tab = $state<CatalogTab>(asTab(params.get('tab')));
	let sub = $state<CatalogSub>(params.get('sub') === 'favorites' ? 'favorites' : 'created');
	let search = $state(params.get('q') ?? '');
	let appliedQ = $state(params.get('q') ?? '');
	let tagIds = $state<string[]>(params.get('tags')?.split(',').filter(Boolean) ?? []);
	let importOpen = $state(params.get('import') === '1');

	const list = new CatalogList();
	let allTags = $state<Named[]>([]);
	let variantToDelete = $state<Extract<CatalogEntry, { kind: 'variant' }> | null>(null);
	let deleteOpen = $state(false);

	let view = $derived<CatalogView>({ tab, sub, q: appliedQ, tagIds });
	let filterable = $derived(isFilterable(view));

	const tabItems = [
		{ value: 'all', label: labels.tabs.all },
		{ value: 'mine', label: labels.tabs.mine },
		{ value: 'variants', label: labels.tabs.variants }
	];

	onMount(async () => {
		reload();
		allTags = await loadNames('tags');
	});

	/** Loads the current view and keeps the address bar in step, so back/forward and shared links land on it. */
	function reload() {
		const current = view;
		void list.load(current);
		const query = new SvelteURLSearchParams();
		if (current.tab !== 'all') query.set('tab', current.tab);
		if (current.tab === 'mine' && current.sub === 'favorites') query.set('sub', 'favorites');
		if (isFilterable(current) && current.q) query.set('q', current.q);
		if (isFilterable(current) && current.tagIds.length > 0) {
			query.set('tags', current.tagIds.join(','));
		}
		const qs = query.toString();
		const target = resolve('/przepisy') + (qs ? `?${qs}` : '');
		if (target !== page.url.pathname + page.url.search) {
			// eslint-disable-next-line svelte/no-navigation-without-resolve -- built from resolve() above
			void goto(target, { replaceState: true, keepFocus: true, noScroll: true });
		}
	}

	let timer: ReturnType<typeof setTimeout> | undefined;
	function onSearchInput() {
		clearTimeout(timer);
		timer = setTimeout(() => {
			appliedQ = search.trim();
			reload();
		}, 300);
	}

	function toggleTag(id: string) {
		tagIds = tagIds.includes(id) ? tagIds.filter((x) => x !== id) : [...tagIds, id];
		reload();
	}

	function unfavorited(entry: CatalogEntry) {
		if (!(tab === 'mine' && sub === 'favorites')) return;
		const kind = entry.kind;
		const id = entry.kind === 'recipe' ? entry.recipe.id : entry.variant.id;
		const index = list.remove(entry.key);
		toast.show(labels.favoriteRemoved, {
			action: {
				label: t.common.actions.undo,
				onAction: async () => {
					try {
						await setFavorite(kind, id, true);
						list.insert(index, entry);
					} catch {
						toast.error(labels.favoriteFailed);
					}
				}
			}
		});
	}

	async function deleteVariant() {
		const entry = variantToDelete;
		if (!entry) return;
		try {
			await unwrap(
				createApi().DELETE('/variants/{id}', { params: { path: { id: entry.variant.id } } })
			);
		} catch (error) {
			toast.error(t.errors['server.error']);
			throw error;
		}
		list.remove(entry.key);
		toast.success(labels.variantDeleted);
	}

	let emptyKind = $derived.by((): keyof typeof empties => {
		if (tab === 'variants') return 'variants';
		if (tab === 'mine' && sub === 'favorites') return 'favorites';
		if (appliedQ || tagIds.length > 0) return 'search';
		return tab === 'mine' ? 'created' : 'all';
	});
	const empties = {
		variants: { title: labels.emptyVariantsTitle, text: labels.emptyVariantsText },
		favorites: { title: labels.emptyFavoritesTitle, text: labels.emptyFavoritesText },
		search: { title: labels.emptySearchTitle, text: labels.emptySearchText },
		created: { title: labels.emptyCreatedTitle, text: labels.emptyCreatedText },
		all: { title: labels.emptyAllTitle, text: labels.emptyAllText }
	} as const;
</script>

<Page title={t.recipes.title} width="wide">
	{#snippet actions()}
		<IconButton icon={Upload} label={labels.import} onclick={() => (importOpen = true)} />
		<IconButton icon={Plus} label={labels.newRecipe} href={resolve('/przepisy/nowy')} />
	{/snippet}

	<Tabs
		items={tabItems}
		label={labels.tabsLabel}
		value={tab}
		onValueChange={(v) => {
			tab = v as CatalogTab;
			reload();
		}}
	>
		{#snippet panel()}
			<div class="flex flex-col gap-4">
				{#if tab === 'mine'}
					<div role="group" aria-label={labels.subLabel} class="flex gap-2">
						<Chip
							selected={sub === 'created'}
							onclick={() => {
								sub = 'created';
								reload();
							}}
						>
							{labels.sub.created}
						</Chip>
						<Chip
							selected={sub === 'favorites'}
							onclick={() => {
								sub = 'favorites';
								reload();
							}}
						>
							{labels.sub.favorites}
						</Chip>
					</div>
				{/if}

				{#if filterable}
					<TextField
						label={labels.search}
						hideLabel
						optional
						type="search"
						placeholder={labels.searchPlaceholder}
						bind:value={search}
						oninput={onSearchInput}
					/>
					{#if allTags.length > 0}
						<div role="group" aria-label={labels.tagFilter} class="flex flex-wrap gap-2">
							{#each allTags as tag (tag.id)}
								<Chip selected={tagIds.includes(tag.id)} onclick={() => toggleTag(tag.id)}>
									{tag.name}
								</Chip>
							{/each}
						</div>
					{/if}
				{/if}

				{#if list.error}
					<ErrorMessage error={list.error} retry={() => list.load(view)} variant="block" />
				{:else if list.loading}
					<div class="grid gap-3 md:grid-cols-2" aria-busy="true">
						{#each [1, 2, 3, 4] as n (n)}
							<Skeleton shape="block" class="h-30 w-full" />
						{/each}
					</div>
				{:else if list.entries.length === 0}
					{@const empty = empties[emptyKind]}
					<EmptyState
						title={empty.title}
						description={empty.text}
						icon={emptyKind === 'search' ? SearchX : undefined}
					>
						{#snippet action()}
							{#if emptyKind !== 'favorites' && emptyKind !== 'variants'}
								<Button icon={Plus} href={resolve('/przepisy/nowy')}>{labels.newRecipe}</Button>
							{/if}
						{/snippet}
					</EmptyState>
				{:else}
					<ul class="grid gap-3 md:grid-cols-2">
						{#each list.entries as entry (entry.key)}
							<li class="contents">
								{#if entry.kind === 'recipe'}
									<RecipeCard
										bind:recipe={entry.recipe}
										onfavorite={(on) => !on && unfavorited(entry)}
									/>
								{:else}
									<VariantCard
										bind:variant={entry.variant}
										onfavorite={(on) => !on && unfavorited(entry)}
										ondelete={() => {
											variantToDelete = entry;
											deleteOpen = true;
										}}
									/>
								{/if}
							</li>
						{/each}
					</ul>
					{#if list.hasMore}
						<div class="flex justify-center">
							<Button
								variant="secondary"
								loading={list.loadingMore}
								onclick={() => list.more(view)}
							>
								{labels.showMore}
							</Button>
						</div>
					{/if}
				{/if}
			</div>
		{/snippet}
	</Tabs>
</Page>

<ImportDialog bind:open={importOpen} />

<ConfirmDialog
	bind:open={deleteOpen}
	title={labels.deleteVariantTitle}
	description={variantToDelete ? labels.deleteVariantText(variantToDelete.variant.name) : undefined}
	confirmLabel={labels.deleteVariant}
	onConfirm={deleteVariant}
/>
