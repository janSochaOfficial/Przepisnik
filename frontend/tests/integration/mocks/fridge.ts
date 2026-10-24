// Feature fixtures (owned by F6): fridges, items, invites and deductions. Typed with Schemas from ./types.
import { me } from './auth';
import { cardamom, flour, milk, onion } from './catalog';
import type { MockHandlers } from './index';
import { problem } from './index';
import type { Schemas } from './types';

export const fridgeId = '00000000-0000-7000-8000-0000000000f1';
export const otherFridgeId = '00000000-0000-7000-8000-0000000000f2';
export const owner: Schemas['UserRef'] = { id: me.id, nickname: 'ania' };

export const summary: Schemas['FridgeSummary'] = {
	id: fridgeId,
	name: 'Moja lodówka',
	role: 'owner',
	owner,
	memberCount: 1,
	itemCount: 2
};
export const otherSummary: Schemas['FridgeSummary'] = {
	...summary,
	id: otherFridgeId,
	name: 'Dom rodziców'
};

export const detail: Schemas['FridgeDetail'] = {
	...summary,
	members: [{ id: me.id, nickname: 'ania', role: 'owner' }]
};

const stamp = '2026-10-01T10:00:00Z';
export const item = (
	id: string,
	product: Schemas['ProductRef'],
	packageAmount: number,
	remainingPercent: number,
	over: Partial<Schemas['FridgeItem']> = {}
): Schemas['FridgeItem'] => ({
	id,
	version: 1,
	product,
	packageAmount,
	remainingPercent,
	remainingAmount: (packageAmount * remainingPercent) / 100,
	note: null,
	addedBy: owner,
	createdAt: stamp,
	updatedAt: stamp,
	...over
});

export const cream = {
	...milk,
	id: '00000000-0000-7000-8000-00000000c001',
	name: 'smietanka',
	defaultPackageAmount: 330
} satisfies Schemas['ProductRef'];

export const creamItem = item('00000000-0000-7000-8000-0000000d0001', cream, 330, 70);
export const flourItem = item('00000000-0000-7000-8000-0000000d0002', flour, 1000, 50);
export const eggs = {
	...onion,
	id: '00000000-0000-7000-8000-00000000c003',
	name: 'jajka',
	hasSizes: false
} satisfies Schemas['ProductRef'];
export const eggItem = item('00000000-0000-7000-8000-0000000d0003', eggs, 10, 60);

export const invite: Schemas['Invite'] = {
	id: '00000000-0000-7000-8000-0000000a0001',
	token: 'tok123',
	url: '/zaproszenie/tok123',
	expiresAt: '2026-11-01T10:00:00Z',
	createdAt: stamp
};

/** Fridges, items and invites with simple in-memory behaviour for the add/edit/delete flows. */
export function fridgeHandlers(
	initial: Schemas['FridgeItem'][] = [creamItem, flourItem]
): MockHandlers {
	let items = [...initial];
	let invites: Schemas['Invite'][] = [];
	let next = 100;
	return {
		'GET /fridges': { body: [summary, otherSummary] },
		'POST /fridges': (ctx) => ({
			status: 201,
			body: {
				...summary,
				id: '00000000-0000-7000-8000-0000000000f9',
				name: (ctx.json as { name: string }).name
			}
		}),
		'GET /fridges/:id': { body: detail },
		'DELETE /fridges/:id': { status: 204 },
		'GET /fridges/:id/items': () => ({ body: items }),
		'POST /fridges/:id/items': (ctx) => {
			const w = ctx.json as Schemas['FridgeItemWrite'];
			const product =
				[cream, flour, milk, cardamom, eggs].find((p) => p.id === w.productId) ?? flour;
			const created = item(
				`00000000-0000-7000-8000-0000000d0${next++}`,
				product,
				w.packageAmount,
				w.remainingPercent,
				{ note: w.note ?? null }
			);
			items = [...items, created];
			return { status: 201, body: created };
		},
		'PUT /fridges/:id/items/:itemId': (ctx) => {
			const w = ctx.json as Schemas['FridgeItemWrite'] & { version: number };
			if (w.remainingPercent === 0) {
				items = items.filter((i) => i.id !== ctx.params.itemId);
				return { status: 204 };
			}
			const old = items.find((i) => i.id === ctx.params.itemId);
			if (!old) return problem(404, 'fridge_item.not_found');
			const updated = item(old.id, old.product, w.packageAmount, w.remainingPercent, {
				version: 2,
				note: w.note ?? null
			});
			items = items.map((i) => (i.id === old.id ? updated : i));
			return { body: updated };
		},
		'DELETE /fridges/:id/items/:itemId': (ctx) => {
			items = items.filter((i) => i.id !== ctx.params.itemId);
			return { status: 204 };
		},
		'GET /fridges/:id/invites': () => ({ body: invites }),
		'POST /fridges/:id/invites': () => {
			invites = [invite];
			return { status: 201, body: invite };
		},
		'DELETE /fridges/:id/invites/:inviteId': () => {
			invites = [];
			return { status: 204 };
		}
	};
}

/** Deduction endpoints; `undo` decides the answer of the undo call. */
export function deductionHandlers(
	undo: Schemas['DeductionResult'] | ReturnType<typeof problem> = {
		id: 'd1',
		undone: true,
		createdAt: stamp,
		lines: []
	}
): MockHandlers {
	return {
		'POST /fridges/:id/deductions': (ctx) => {
			const req = ctx.json as { lines: { productId: string; amount: number }[] };
			return {
				status: 201,
				body: {
					id: '00000000-0000-7000-8000-0000000cd001',
					undone: false,
					createdAt: stamp,
					lines: req.lines.map((l) => ({
						productId: l.productId,
						requested: l.amount,
						deducted: l.amount,
						shortfall: 0,
						items: []
					}))
				}
			};
		},
		'POST /fridges/:id/deductions/:deductionId/undo': 'status' in undo ? undo : { body: undo }
	};
}
