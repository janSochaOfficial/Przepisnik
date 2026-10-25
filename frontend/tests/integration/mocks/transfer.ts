// Feature fixtures (I1): importing a file whose recipe already exists as variants (PROJECT §4.7, D-35).
// Typed with Schemas from ./types; export MockHandlers for mockApi().
import type { MockHandlers } from './index';
import { ania, bartek, sampleExport, summary, variantSummary } from './recipes';
import type { Schemas } from './types';

export const ownMatchId = '00000000-0000-7000-8000-0000000e0101';
export const publicMatchId = '00000000-0000-7000-8000-0000000e0102';
export const importedVariantId = '00000000-0000-7000-8000-0000000b0101';

/** Two recipes titled like the sample export: the caller's own first, then someone else's public one. */
export const importMatches: Schemas['RecipeSummary'][] = [
	summary({
		id: ownMatchId,
		title: sampleExport.recipe.title,
		author: ania,
		visibility: 'private'
	}),
	summary({
		id: publicMatchId,
		title: sampleExport.recipe.title,
		author: bartek,
		visibility: 'public'
	})
];

export function variantImportResult(
	recipeId: string,
	over: Partial<Schemas['VariantImportResult']> = {}
): Schemas['VariantImportResult'] {
	return {
		recipeId,
		variants: [
			variantSummary({
				id: importedVariantId,
				recipeId,
				recipeTitle: sampleExport.recipe.title,
				name: `Wersja @${sampleExport.recipe.authorNickname}`
			})
		],
		skipped: 0,
		...over
	};
}

/**
 * Matching and import handlers. Spread them **before** other recipe handlers: `GET /recipes/:id`
 * would otherwise also answer `GET /recipes/import-matches`.
 */
export function transferHandlers(
	matches: Schemas['RecipeSummary'][] = importMatches
): MockHandlers {
	return {
		'GET /recipes/import-matches': { body: matches },
		'POST /recipes/:id/variants/import': ({ params }) => ({
			status: 201,
			body: variantImportResult(params.id as string)
		})
	};
}
