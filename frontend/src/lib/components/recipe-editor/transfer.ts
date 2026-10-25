// Export and import of recipes as JSON files (PROJECT §4.7, API §8).
import { ApiError, createApi, toApiError, unwrap } from '$lib/api/client';
import type { components } from '$lib/api/schema';

type RecipeExport = components['schemas']['RecipeExport'];
type RecipeDetail = components['schemas']['RecipeDetail'];
type RecipeSummary = components['schemas']['RecipeSummary'];
type VariantImportResult = components['schemas']['VariantImportResult'];

/** The longest title `GET /recipes/import-matches` accepts (API §4). */
export const MAX_MATCH_TITLE = 150;

/** The file name from a `Content-Disposition` header, if any. */
export function fileNameFrom(header: string | null, fallback: string): string {
	const match = header ? /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header) : null;
	return match?.[1] ? decodeURIComponent(match[1]) : fallback;
}

/** Saves `blob` as a download in the browser. */
function download(blob: Blob, name: string): void {
	const url = URL.createObjectURL(blob);
	const link = document.createElement('a');
	link.href = url;
	link.download = name;
	document.body.append(link);
	link.click();
	link.remove();
	setTimeout(() => URL.revokeObjectURL(url), 1000);
}

/** Downloads `GET /recipes/{id}/export` as a file. Throws an {@link ApiError}. */
export async function exportRecipe(id: string): Promise<void> {
	const { data, error, response } = await createApi().GET('/recipes/{id}/export', {
		params: { path: { id } },
		parseAs: 'blob'
	});
	if (error !== undefined || !response.ok || !data) throw toApiError(response.status, error);
	download(data as Blob, fileNameFrom(response.headers.get('Content-Disposition'), `${id}.json`));
}

/** Reads and parses a picked file; a file that is not JSON is `import.invalid_format`. */
export async function readExportFile(file: File): Promise<RecipeExport> {
	try {
		return JSON.parse(await file.text()) as RecipeExport;
	} catch {
		throw new ApiError(400, 'import.invalid_format');
	}
}

/**
 * The title to look for existing recipes with (PROJECT §4.7, D-35), or `null` when the file has
 * none the matching endpoint accepts; such a file goes straight to the normal import, which reports it.
 */
export function matchTitle(file: unknown): string | null {
	const recipe = (file as { recipe?: unknown } | null)?.recipe;
	const title = (recipe as { title?: unknown } | null | undefined)?.title;
	if (typeof title !== 'string') return null;
	const trimmed = title.trim();
	return trimmed.length >= 1 && trimmed.length <= MAX_MATCH_TITLE ? trimmed : null;
}

/** `GET /recipes/import-matches`: visible, non-archived recipes with the same normalized title. */
export async function findImportMatches(title: string): Promise<RecipeSummary[]> {
	return unwrap(createApi().GET('/recipes/import-matches', { params: { query: { title } } }));
}

/** `POST /recipes/import` with a parsed export file. Resolves to the new (private) recipe. */
export async function importRecipe(body: RecipeExport): Promise<RecipeDetail> {
	return unwrap(createApi().POST('/recipes/import', { body }));
}

/** `POST /recipes/{id}/variants/import`: the file's recipe and variants become private variants of `recipeId`. */
export async function importAsVariants(
	recipeId: string,
	body: RecipeExport
): Promise<VariantImportResult> {
	return unwrap(
		createApi().POST('/recipes/{id}/variants/import', { params: { path: { id: recipeId } }, body })
	);
}
