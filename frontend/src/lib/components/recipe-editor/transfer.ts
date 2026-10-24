// Export and import of recipes as JSON files (PROJECT §4.7, API §8).
import { ApiError, createApi, toApiError, unwrap } from '$lib/api/client';
import type { components } from '$lib/api/schema';

type RecipeExport = components['schemas']['RecipeExport'];
type RecipeDetail = components['schemas']['RecipeDetail'];

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

/** `POST /recipes/import` with the contents of `file`. Resolves to the new (private) recipe. */
export async function importRecipe(file: File): Promise<RecipeDetail> {
	const body = await readExportFile(file);
	return unwrap(createApi().POST('/recipes/import', { body }));
}
