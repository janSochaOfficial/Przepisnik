import { SvelteMap } from 'svelte/reactivity';
import { createApi, unwrap } from '$lib/api/client';
import type { components } from '$lib/api/schema';
import type { ShoppingListItem } from '$lib/domain/types';

type Schemas = components['schemas'];
export type ShoppingListItemWrite = Schemas['ShoppingListItemWrite'];

/** Unchecked first; otherwise the server's order (category, name) is kept. */
export function sortItems(items: ShoppingListItem[]): ShoppingListItem[] {
	return [...items.filter((i) => !i.checked), ...items.filter((i) => i.checked)];
}

/**
 * The shopping list of one fridge. Check-off is optimistic: the item flips at once and is sent in the
 * background. Calls on the same item run one after another, so each carries the version the server
 * just returned (otherwise a quick double tap would answer 409).
 */
export class ShoppingListState {
	items = $state<ShoppingListItem[]>([]);
	loading = $state(true);
	error = $state<unknown>(null);

	#fridgeId = '';
	#request = 0;
	#chains = new SvelteMap<string, Promise<unknown>>();
	#pending = new SvelteMap<string, number>();

	get unchecked(): ShoppingListItem[] {
		return this.items.filter((i) => !i.checked);
	}

	get checked(): ShoppingListItem[] {
		return this.items.filter((i) => i.checked);
	}

	/** Loads (or reloads) the list of `fridgeId`. A newer call makes older answers irrelevant. */
	async load(fridgeId: string, options: { quiet?: boolean } = {}): Promise<void> {
		const current = ++this.#request;
		if (fridgeId !== this.#fridgeId) this.items = [];
		this.#fridgeId = fridgeId;
		if (!options.quiet) this.loading = true;
		this.error = null;
		try {
			const items = await unwrap(
				createApi().GET('/fridges/{id}/shopping-list', { params: { path: { id: fridgeId } } })
			);
			if (current === this.#request) this.items = sortItems(items);
		} catch (e) {
			if (current === this.#request) this.error = e;
		} finally {
			if (current === this.#request) this.loading = false;
		}
	}

	#replace(item: ShoppingListItem): void {
		this.items = sortItems(this.items.map((i) => (i.id === item.id ? item : i)));
	}

	#queue<T>(id: string, run: () => Promise<T>): Promise<T> {
		const previous = this.#chains.get(id) ?? Promise.resolve();
		const next = previous.then(run, run);
		this.#chains.set(
			id,
			next.catch(() => undefined)
		);
		return next;
	}

	/** Flips `checked`. Throws when the server refuses; the item is then put back (409 reloads the list). */
	async toggle(id: string): Promise<void> {
		const item = this.items.find((i) => i.id === id);
		if (!item) return;
		const desired = !item.checked;
		this.#replace({ ...item, checked: desired });
		this.#pending.set(id, (this.#pending.get(id) ?? 0) + 1);
		try {
			await this.#queue(id, async () => {
				const latest = this.items.find((i) => i.id === id);
				if (!latest) return;
				const saved = await unwrap(
					createApi().PATCH('/fridges/{id}/shopping-list/items/{itemId}', {
						params: { path: { id: this.#fridgeId, itemId: id } },
						body: { version: latest.version, checked: desired }
					})
				);
				// Keep the local flag while another tap is still on its way.
				const more = (this.#pending.get(id) ?? 1) > 1;
				this.#replace(
					more ? { ...saved, checked: this.items.find((i) => i.id === id)!.checked } : saved
				);
			});
		} catch (e) {
			const now = this.items.find((i) => i.id === id);
			if (now) this.#replace({ ...now, checked: !desired });
			void this.load(this.#fridgeId, { quiet: true });
			throw e;
		} finally {
			this.#pending.set(id, (this.#pending.get(id) ?? 1) - 1);
		}
	}

	/** Changes the amount of a product entry or the text of a free-text entry. */
	async patch(id: string, change: { amount?: number; text?: string }): Promise<void> {
		await this.#queue(id, async () => {
			const latest = this.items.find((i) => i.id === id);
			if (!latest) return;
			try {
				const saved = await unwrap(
					createApi().PATCH('/fridges/{id}/shopping-list/items/{itemId}', {
						params: { path: { id: this.#fridgeId, itemId: id } },
						body: { version: latest.version, ...change }
					})
				);
				this.#replace(saved);
			} catch (e) {
				void this.load(this.#fridgeId, { quiet: true });
				throw e;
			}
		});
	}

	/** Adds entries (the server merges equal products) and reloads the list. */
	async add(entries: ShoppingListItemWrite[]): Promise<void> {
		await unwrap(
			createApi().POST('/fridges/{id}/shopping-list/items', {
				params: { path: { id: this.#fridgeId } },
				body: { items: entries }
			})
		);
		await this.load(this.#fridgeId, { quiet: true });
	}

	/** Move the bought entries to the fridge, or just remove them. */
	async complete(moveToFridge: boolean): Promise<{ removed: number; moved: number }> {
		const result = await unwrap(
			createApi().POST('/fridges/{id}/shopping-list/complete', {
				params: { path: { id: this.#fridgeId } },
				body: { moveToFridge }
			})
		);
		await this.load(this.#fridgeId, { quiet: true });
		return { removed: result.removed, moved: result.createdFridgeItems.length };
	}
}
