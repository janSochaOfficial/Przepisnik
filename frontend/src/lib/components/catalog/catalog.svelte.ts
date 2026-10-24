import { createApi, unwrap } from '$lib/api/client';
import type { components } from '$lib/api/schema';

type Schemas = components['schemas'];

export type CatalogTab = 'all' | 'mine' | 'variants';
export type CatalogSub = 'created' | 'favorites';

/** What the list shows: the tab, the "Moje przepisy" switch and the filters. */
export type CatalogView = {
	tab: CatalogTab;
	sub: CatalogSub;
	q: string;
	tagIds: string[];
};

export type CatalogEntry =
	| { key: string; kind: 'recipe'; recipe: Schemas['RecipeSummary'] }
	| {
			key: string;
			kind: 'variant';
			variant: Schemas['VariantSummary'];
			recipe: Schemas['RecipeSummary'] | null;
	  };

const PAGE_SIZE = 20;

/** Whether the search box and tag chips apply to this view (only `GET /recipes` filters). */
export function isFilterable(view: Pick<CatalogView, 'tab' | 'sub'>): boolean {
	return view.tab === 'all' || (view.tab === 'mine' && view.sub === 'created');
}

/** Paged list state for the catalog. A newer `load` makes older responses irrelevant. */
export class CatalogList {
	entries = $state<CatalogEntry[]>([]);
	totalCount = $state(0);
	loading = $state(false);
	loadingMore = $state(false);
	error = $state<unknown>(null);

	#page = 0;
	#controller: AbortController | undefined;

	get hasMore(): boolean {
		return this.entries.length < this.totalCount;
	}

	/** Loads page 1 of `view`, replacing the list. */
	async load(view: CatalogView): Promise<void> {
		this.#controller?.abort();
		this.loading = true;
		this.loadingMore = false;
		this.error = null;
		await this.#fetch(view, 1);
	}

	/** Appends the next page. */
	async more(view: CatalogView): Promise<void> {
		if (this.loading || this.loadingMore || !this.hasMore) return;
		this.#controller?.abort();
		this.loadingMore = true;
		await this.#fetch(view, this.#page + 1);
	}

	remove(key: string): number {
		const index = this.entries.findIndex((e) => e.key === key);
		if (index >= 0) {
			this.entries = this.entries.filter((e) => e.key !== key);
			this.totalCount = Math.max(0, this.totalCount - 1);
		}
		return index;
	}

	insert(index: number, entry: CatalogEntry): void {
		const copy = [...this.entries];
		copy.splice(Math.min(Math.max(index, 0), copy.length), 0, entry);
		this.entries = copy;
		this.totalCount += 1;
	}

	async #fetch(view: CatalogView, page: number): Promise<void> {
		const controller = new AbortController();
		this.#controller = controller;
		const api = createApi();
		const signal = controller.signal;
		try {
			let items: CatalogEntry[];
			let total: number;
			if (view.tab === 'variants') {
				const result = await unwrap(
					api.GET('/variants', {
						params: { query: { author: 'me', page, pageSize: PAGE_SIZE } },
						signal
					})
				);
				items = result.items.map((variant) => ({
					key: `variant:${variant.id}`,
					kind: 'variant',
					variant,
					recipe: null
				}));
				total = result.totalCount;
			} else if (view.tab === 'mine' && view.sub === 'favorites') {
				const result = await unwrap(
					api.GET('/favorites', { params: { query: { page, pageSize: PAGE_SIZE } }, signal })
				);
				items = result.items.map((item) =>
					item.kind === 'recipe'
						? { key: `recipe:${item.recipe.id}`, kind: 'recipe', recipe: item.recipe }
						: {
								key: `variant:${item.variant.id}`,
								kind: 'variant',
								variant: item.variant,
								recipe: item.recipe
							}
				);
				total = result.totalCount;
			} else {
				const query = {
					page,
					pageSize: PAGE_SIZE,
					...(view.q ? { q: view.q } : {}),
					...(view.tagIds.length > 0 ? { tags: view.tagIds.join(',') } : {}),
					...(view.tab === 'mine' ? { author: 'me' } : {})
				};
				const result = await unwrap(api.GET('/recipes', { params: { query }, signal }));
				items = result.items.map((recipe) => ({
					key: `recipe:${recipe.id}`,
					kind: 'recipe',
					recipe
				}));
				total = result.totalCount;
			}
			if (signal.aborted) return;
			this.entries = page === 1 ? items : [...this.entries, ...items];
			this.totalCount = total;
			this.#page = page;
		} catch (error) {
			if (signal.aborted) return;
			this.error = error;
		} finally {
			if (!signal.aborted) {
				this.loading = false;
				this.loadingMore = false;
			}
		}
	}
}
