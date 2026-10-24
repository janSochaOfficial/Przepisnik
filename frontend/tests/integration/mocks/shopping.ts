// Feature fixtures (owned by F6): the shopping list. Typed with Schemas from ./types.
import { milk, flour } from './catalog';
import { owner } from './fridge';
import type { MockHandlers } from './index';
import type { Schemas } from './types';

export const entry = (
	id: string,
	over: Partial<Schemas['ShoppingListItem']>
): Schemas['ShoppingListItem'] => ({
	id: `00000000-0000-7000-8000-0000000b5${id}`,
	version: 1,
	product: null,
	text: null,
	amount: null,
	checked: false,
	addedBy: owner,
	createdAt: '2026-10-01T10:00:00Z',
	...over
});

export const milkEntry = entry('01', { product: milk, amount: 1500 });
export const flourEntry = entry('02', { product: flour, amount: 500, checked: true });
export const textEntry = entry('03', { text: 'papier do pieczenia' });

export function shoppingHandlers(initial = [milkEntry, textEntry, flourEntry]): MockHandlers {
	let items = [...initial];
	return {
		'GET /fridges/:id/shopping-list': () => ({ body: items }),
		'PATCH /fridges/:id/shopping-list/items/:itemId': (ctx) => {
			const patch = ctx.json as { checked?: boolean };
			const old = items.find((i) => i.id === ctx.params.itemId)!;
			const updated = { ...old, ...patch, version: old.version + 1 };
			items = items.map((i) => (i.id === old.id ? updated : i));
			return { body: updated };
		},
		'POST /fridges/:id/shopping-list/items': (ctx) => {
			const added = (ctx.json as { items: { text?: string }[] }).items.map((w, n) =>
				entry(`1${n}`, { text: w.text ?? 'x' })
			);
			items = [...items, ...added];
			return { body: added };
		},
		'POST /fridges/:id/shopping-list/complete': () => {
			const removed = items.filter((i) => i.checked).length;
			items = items.filter((i) => !i.checked);
			return { body: { removed, createdFridgeItems: [] } };
		}
	};
}
