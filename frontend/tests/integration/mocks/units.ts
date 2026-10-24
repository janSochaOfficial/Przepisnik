// Feature fixtures (owned by U1): custom units (PROJECT §5.6) and a recipe that uses them. Typed with
// Schemas from ./types; export MockHandlers for mockApi().
import { milk } from './catalog';
import type { MockHandlers } from './index';
import { ania, detail, recipeId } from './recipes';
import type { Schemas } from './types';

type Ingredient = Schemas['Ingredient'];

/** Chopped tomatoes: a mass product without density. */
export const tomatoes: Schemas['ProductRef'] = {
	id: '00000000-0000-7000-8000-00000000a201',
	name: 'pomidory krojone',
	category: 'canned',
	measureKind: 'mass',
	displayClass: 'weight',
	hasSizes: false,
	allowHalf: false,
	gramsPerMl: null,
	gramsPerPiece: null,
	defaultPackageAmount: null,
	pieceForms: null,
	isVerified: true
};

/** 1 puszka of chopped tomatoes = 400 g (a unit for one product). */
export const can: Schemas['CustomUnitRef'] = {
	id: '00000000-0000-7000-8000-0000000c0001',
	name: 'puszka',
	forms: { one: 'puszka', few: 'puszki', many: 'puszek', fraction: 'puszki' },
	productId: tomatoes.id,
	measureKind: 'mass',
	amount: 400
};

/** 1 filiżanka = 150 ml (a unit for every product). */
export const cup: Schemas['CustomUnitRef'] = {
	id: '00000000-0000-7000-8000-0000000c0002',
	name: 'filiżanka',
	forms: { one: 'filiżanka', few: 'filiżanki', many: 'filiżanek', fraction: 'filiżanki' },
	productId: null,
	measureKind: 'volume',
	amount: 150
};

const ids = {
	tomatoes: '00000000-0000-7000-8000-0000000f2001',
	milk: '00000000-0000-7000-8000-0000000f2002'
};

const iTomatoes: Ingredient = {
	id: ids.tomatoes,
	product: tomatoes,
	quantity: { value: 2, unit: 'custom', customUnitId: can.id, customUnit: can },
	baseAmount: 800,
	note: null,
	optional: false
};
const iMilk: Ingredient = {
	id: ids.milk,
	product: milk,
	quantity: { value: 1, unit: 'custom', customUnitId: cup.id, customUnit: cup },
	baseAmount: 150,
	note: null,
	optional: false
};

/** A recipe with "2 puszki" of tomatoes and "1 filiżanka" of milk, and no variants. */
export const sauce: Schemas['RecipeDetail'] = detail({
	title: 'Sos pomidorowy',
	ingredients: [iTomatoes, iMilk],
	instructionSteps: [
		{ id: '00000000-0000-7000-8000-0000000a2001', blocks: [{ type: 'text', text: 'Gotuj.' }] }
	],
	variantCount: 0
});

const sauceMatrix: Schemas['VariantMatrix'] = {
	recipeId,
	variants: [
		{
			id: 'base',
			name: 'base',
			author: ania,
			cookCount: 0,
			createdAt: '2026-10-01T10:00:00Z',
			isFavorite: false,
			productSwaps: []
		}
	],
	slots: [iTomatoes, iMilk].map((i) => ({
		key: `i:${i.id}`,
		label: i.product.name,
		afterSlotKey: null,
		options: [{ key: `${i.product.id}:${i.baseAmount}`, ingredient: i, variantIds: ['base'] }]
	}))
};

/** The unit catalog; `POST /units` answers with `created`. */
export function unitsHandlers(created: Schemas['CustomUnitRef'] = can): MockHandlers {
	return {
		'GET /units': ({ url }) => {
			const productId = url.searchParams.get('productId');
			return { body: [can, cup].filter((u) => u.productId === null || u.productId === productId) };
		},
		'POST /units': { status: 201, body: created }
	};
}

/** The recipe view of `sauce`. */
export const sauceHandlers: MockHandlers = {
	'GET /recipes/:id': { body: sauce },
	'GET /recipes/:id/variant-matrix': { body: sauceMatrix },
	'GET /recipes/:id/my-selection': { status: 204 },
	'GET /me/product-preferences': { body: [] },
	'PUT /recipes/:id/my-selection': { status: 200, body: {} }
};
