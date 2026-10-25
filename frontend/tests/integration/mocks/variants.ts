// Feature fixtures (owned by F5): a recipe with three variants, its matrix, resolved variants and
// the selection memory. Typed with Schemas from ./types; export MockHandlers for mockApi().
//
// The matrix has the same shape as the F2 matching vectors (`shared/test-vectors/matching/F2.json`:
// base + variants A, B, C, base-slot keys `i:<ingredientId>`, option keys `<productId>:<amount>`),
// with real UUIDs and complete products so the recipe view can format it.
import { cardamom, milk, onion } from './catalog';
import type { MockHandlers } from './index';
import { ania, bartek, detail, recipeId, variantSummary } from './recipes';
import type { Schemas } from './types';

type Ingredient = Schemas['Ingredient'];

const product = (
	id: string,
	name: string,
	over: Partial<Schemas['ProductRef']> = {}
): Schemas['ProductRef'] => ({
	id,
	name,
	category: 'other',
	measureKind: 'mass',
	displayClass: 'weight',
	hasSizes: false,
	allowHalf: false,
	gramsPerMl: null,
	gramsPerPiece: null,
	defaultPackageAmount: null,
	pieceForms: null,
	isVerified: true,
	...over
});

export const chicken = product('00000000-0000-7000-8000-00000000a101', 'kurczak', {
	category: 'meat'
});
export const tofu = product('00000000-0000-7000-8000-00000000a102', 'tofu', { category: 'dairy' });
export const cream = product('00000000-0000-7000-8000-00000000a103', 'śmietanka', {
	category: 'dairy',
	measureKind: 'volume',
	displayClass: 'liquid',
	gramsPerMl: 1
});
export const pepper = product('00000000-0000-7000-8000-00000000a104', 'papryka', {
	category: 'vegetables',
	measureKind: 'count',
	displayClass: 'piece',
	gramsPerPiece: 150
});

export const variantA = '00000000-0000-7000-8000-0000000b0a01';
export const variantB = '00000000-0000-7000-8000-0000000b0b01';
export const variantC = '00000000-0000-7000-8000-0000000b0c01';

const ids = {
	chicken: '00000000-0000-7000-8000-0000000f1001',
	milk: '00000000-0000-7000-8000-0000000f1002',
	onion: '00000000-0000-7000-8000-0000000f1003',
	cardamom: '00000000-0000-7000-8000-0000000f1004',
	pepper: '00000000-0000-7000-8000-0000000f1005'
};

const row = (
	id: string,
	p: Schemas['ProductRef'],
	quantity: Schemas['Quantity'],
	baseAmount: number,
	note: string | null = null
): Ingredient => ({ id, product: p, quantity, baseAmount, note, optional: false });

const iChicken = row(ids.chicken, chicken, { value: 400, unit: 'g' }, 400);
const iTofu = row(ids.chicken, tofu, { value: 400, unit: 'g' }, 400);
const iMilk = row(ids.milk, milk, { value: 1, unit: 'glass' }, 250);
const iCream = row(ids.milk, cream, { value: 1, unit: 'glass' }, 250);
const iOnion = row(ids.onion, onion, { value: 2, unit: 'piece', size: 'large' }, 2.6);
const iCardamom = row(ids.cardamom, cardamom, { value: 1, unit: 'tsp' }, 5);
const iCardamomA = row(ids.cardamom, cardamom, { value: 2, unit: 'tsp' }, 10);
const iPepper = row(ids.pepper, pepper, { value: 1, unit: 'piece' }, 1);

const text = (s: string): Schemas['Step'] => ({
	id: '00000000-0000-7000-8000-0000000a0001',
	blocks: [{ type: 'text', text: s }]
});

/** The base recipe: chicken, milk, onion, cardamom. */
export function baseRecipe(over: Partial<Schemas['RecipeDetail']> = {}): Schemas['RecipeDetail'] {
	return detail({
		title: 'Gulasz z kurczaka',
		ingredients: [iChicken, iMilk, iOnion, iCardamom],
		instructionSteps: [text('Krok bazowy: podsmaż kurczaka.')],
		variantCount: 3,
		...over
	});
}

const resolvedOf = (
	variantId: string,
	name: string,
	ingredients: Ingredient[],
	step: string
): Schemas['ResolvedRecipe'] => {
	const {
		totalVariantCount,
		foreignVariantCount,
		canEdit,
		version,
		variantCount,
		myLastCountedCookAt,
		...rest
	} = baseRecipe();
	void [
		totalVariantCount,
		foreignVariantCount,
		canEdit,
		version,
		variantCount,
		myLastCountedCookAt
	];
	return {
		...rest,
		ingredients,
		instructionSteps: [text(step)],
		variantId,
		variantName: name
	};
};

export const resolved: Record<string, Schemas['ResolvedRecipe']> = {
	[variantA]: resolvedOf(
		variantA,
		'Wersja wege',
		[iTofu, iMilk, iOnion, iCardamomA],
		'Krok wariantu A: podsmaż tofu.'
	),
	[variantB]: resolvedOf(
		variantB,
		'Kremowa',
		[iChicken, iCream, iOnion, iCardamom],
		'Krok wariantu B: dodaj śmietankę.'
	),
	[variantC]: resolvedOf(
		variantC,
		'Z papryką',
		[iChicken, iPepper, iMilk, iOnion, iCardamom],
		'Krok wariantu C: dodaj paprykę.'
	)
};

const key = (i: Ingredient) => `${i.product.id}:${i.baseAmount}`;
const option = (i: Ingredient | null, variantIds: string[]): Schemas['SlotOption'] => ({
	key: i ? key(i) : 'none',
	ingredient: i,
	variantIds
});

const mv = (
	id: string,
	name: string,
	cookCount: number,
	createdAt: string,
	isFavorite = false
): Schemas['MatrixVariant'] => ({
	id,
	name,
	author: ania,
	cookCount,
	createdAt,
	isFavorite,
	productSwaps: []
});

export function matrix(favorites: string[] = []): Schemas['VariantMatrix'] {
	const fav = (id: string) => favorites.includes(id);
	return {
		recipeId,
		variants: [
			mv('base', 'base', 5, '2026-10-01T10:00:00Z', fav('base')),
			mv(variantA, 'Wersja wege', 2, '2026-10-02T10:00:00Z', fav(variantA)),
			mv(variantB, 'Kremowa', 1, '2026-10-03T10:00:00Z', fav(variantB)),
			mv(variantC, 'Z papryką', 0, '2026-10-04T10:00:00Z', fav(variantC))
		],
		slots: [
			{
				key: `i:${ids.chicken}`,
				label: 'kurczak',
				afterSlotKey: null,
				options: [option(iChicken, ['base', variantB, variantC]), option(iTofu, [variantA])]
			},
			{
				key: `i:${ids.milk}`,
				label: 'mleko',
				afterSlotKey: null,
				options: [option(iMilk, ['base', variantA, variantC]), option(iCream, [variantB])]
			},
			{
				key: `i:${ids.onion}`,
				label: 'cebula',
				afterSlotKey: null,
				options: [option(iOnion, ['base', variantA, variantB, variantC])]
			},
			{
				key: `i:${ids.cardamom}`,
				label: 'kardamon',
				afterSlotKey: null,
				options: [option(iCardamom, ['base', variantB, variantC]), option(iCardamomA, [variantA])]
			},
			{
				// Added by C, right after the chicken (B4's `afterSlotKey`).
				key: `p:${pepper.id}`,
				label: 'papryka',
				afterSlotKey: `i:${ids.chicken}`,
				options: [option(null, ['base', variantA, variantB]), option(iPepper, [variantC])]
			}
		]
	};
}

export const slotKey = {
	chicken: `i:${ids.chicken}`,
	milk: `i:${ids.milk}`,
	cardamom: `i:${ids.cardamom}`,
	pepper: `p:${pepper.id}`
};
export const optionKey = { tofu: key(iTofu), cream: key(iCream), pepper: key(iPepper) };
export const baseIngredientIds = ids;

export function variantDetail(
	over: Partial<Schemas['VariantDetail']> = {}
): Schemas['VariantDetail'] {
	return {
		id: variantA,
		recipeId,
		recipeTitle: 'Gulasz z kurczaka',
		name: 'Wersja wege',
		description: null,
		author: ania,
		visibility: 'private',
		isFavorite: false,
		baseAvailable: true,
		cookCount: 2,
		changeCount: 2,
		createdAt: '2026-10-02T10:00:00Z',
		updatedAt: '2026-10-02T10:00:00Z',
		version: 3,
		ingredientOps: [],
		stepOps: [],
		utensilIdsAdded: [],
		utensilIdsRemoved: [],
		prepMinutes: null,
		cookMinutes: null,
		...over
	};
}

export { bartek };

/** Read-only handlers: a recipe with three variants and no saved selection. */
export const variantsHandlers: MockHandlers = {
	'GET /recipes/:id': { body: baseRecipe() },
	'GET /recipes/:id/variant-matrix': { body: matrix() },
	'GET /recipes/:id/my-selection': { status: 204 },
	'GET /recipes/:id/variants': {
		body: [
			variantSummary({ id: variantA, name: 'Wersja wege', visibility: 'private', cookCount: 2 }),
			variantSummary({ id: variantB, name: 'Kremowa', visibility: 'fridges', cookCount: 1 }),
			variantSummary({ id: variantC, name: 'Z papryką', visibility: 'public', cookCount: 0 })
		]
	},
	'GET /me/product-preferences': { body: [] },
	'GET /variants/:id/resolved': ({ params }) => ({ body: resolved[params.id ?? ''] }),
	'PUT /recipes/:id/my-selection': { status: 200, body: {} }
};

/** Handlers that remember the last `PUT my-selection` and answer it on the next load. */
export function selectionMemory(): { handlers: MockHandlers; saved: () => unknown } {
	let saved: { picks: unknown; scaling?: unknown } | null = null;
	return {
		saved: () => saved,
		handlers: {
			'GET /recipes/:id/my-selection': () =>
				saved
					? {
							body: { recipeId, ...saved, updatedAt: '2026-10-10T10:00:00Z' }
						}
					: { status: 204 },
			'PUT /recipes/:id/my-selection': ({ json }) => {
				saved = json as typeof saved;
				return { body: { recipeId, ...saved, updatedAt: '2026-10-10T10:00:00Z' } };
			}
		}
	};
}
