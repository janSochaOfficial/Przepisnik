// Feature fixtures (owned by F4): recipes, favourites and variants for the catalog and the editor.
// Typed with Schemas from ./types; export MockHandlers for mockApi().
import { flour, milk, onion, tagObiad } from './catalog';
import type { MockHandlers } from './index';
import type { Schemas } from './types';

export const ania: Schemas['UserRef'] = {
	id: '00000000-0000-7000-8000-000000000001',
	nickname: 'ania'
};
export const bartek: Schemas['UserRef'] = {
	id: '00000000-0000-7000-8000-000000000002',
	nickname: 'bartek'
};

export const recipeId = '00000000-0000-7000-8000-0000000e0001';
export const newRecipeId = '00000000-0000-7000-8000-0000000e00ff';

export function summary(over: Partial<Schemas['RecipeSummary']> = {}): Schemas['RecipeSummary'] {
	return {
		id: recipeId,
		title: 'Placki ziemniaczane',
		author: ania,
		coverImage: null,
		visibility: 'private',
		isFavorite: false,
		tags: [tagObiad],
		prepMinutes: 15,
		cookMinutes: 20,
		variantCount: 0,
		createdAt: '2026-10-01T10:00:00Z',
		updatedAt: '2026-10-01T10:00:00Z',
		...over
	};
}

export function detail(over: Partial<Schemas['RecipeDetail']> = {}): Schemas['RecipeDetail'] {
	return {
		id: recipeId,
		version: 7,
		isArchived: false,
		visibility: 'private',
		isFavorite: false,
		canEdit: true,
		totalVariantCount: 0,
		foreignVariantCount: 0,
		title: 'Placki ziemniaczane',
		description: 'Chrupiące placki.',
		coverImage: null,
		author: ania,
		prepMinutes: 15,
		cookMinutes: 20,
		tags: [tagObiad],
		utensils: [],
		ingredients: [
			{
				id: '00000000-0000-7000-8000-0000000f0001',
				product: flour,
				quantity: { value: 200, unit: 'g' },
				baseAmount: 200,
				note: null,
				optional: false
			},
			{
				id: '00000000-0000-7000-8000-0000000f0002',
				product: milk,
				quantity: { value: 1, unit: 'glass' },
				baseAmount: 250,
				note: 'ciepłe',
				optional: false
			},
			{
				id: '00000000-0000-7000-8000-0000000f0003',
				product: onion,
				quantity: { value: 2, unit: 'piece', size: 'large' },
				baseAmount: 2.6,
				note: null,
				optional: true
			}
		],
		prepSteps: [],
		instructionSteps: [
			{
				id: '00000000-0000-7000-8000-0000000a0001',
				blocks: [{ type: 'text', text: 'Zetrzyj ziemniaki i smaż.' }]
			}
		],
		cookCount: 0,
		variantCount: 0,
		createdAt: '2026-10-01T10:00:00Z',
		updatedAt: '2026-10-01T10:00:00Z',
		...over
	};
}

export function variantSummary(
	over: Partial<Schemas['VariantSummary']> = {}
): Schemas['VariantSummary'] {
	return {
		id: '00000000-0000-7000-8000-0000000b0001',
		recipeId,
		recipeTitle: 'Placki ziemniaczane',
		name: 'Wersja wege',
		description: null,
		author: ania,
		visibility: 'private',
		isFavorite: false,
		baseAvailable: true,
		cookCount: 0,
		changeCount: 2,
		createdAt: '2026-10-02T10:00:00Z',
		updatedAt: '2026-10-02T10:00:00Z',
		...over
	};
}

export function paged<T>(items: T[]): {
	items: T[];
	page: number;
	pageSize: number;
	totalCount: number;
} {
	return { items, page: 1, pageSize: 20, totalCount: items.length };
}

/** A minimal export file, as `GET /recipes/{id}/export` would produce it. */
export const sampleExport: Schemas['RecipeExport'] = {
	format: 'recipes-list/recipe',
	formatVersion: 1,
	exportedAt: '2026-10-03T10:00:00Z',
	products: [],
	images: {},
	recipe: {
		key: 'r1',
		title: 'Zaimportowany przepis',
		description: null,
		coverImageKey: null,
		authorNickname: 'bartek',
		prepMinutes: null,
		cookMinutes: null,
		tags: [],
		utensils: [],
		ingredients: [],
		prepSteps: [],
		instructionSteps: []
	},
	variants: []
};

/** Default list handlers: one recipe in every list. Tests spread this and override single routes. */
export const recipesHandlers: MockHandlers = {
	'GET /recipes': { body: paged([summary()]) },
	'GET /recipes/:id': { body: detail() },
	'GET /favorites': { body: paged([]) },
	'GET /variants': { body: paged([]) },
	'PUT /recipes/:id/favorite': { status: 204 },
	'DELETE /recipes/:id/favorite': { status: 204 },
	'PUT /variants/:id/favorite': { status: 204 },
	'DELETE /variants/:id/favorite': { status: 204 }
};
