import { createApi, unwrap } from '$lib/api/client';

export type FavoriteKind = 'recipe' | 'variant';

/** Marks (`on`) or unmarks a recipe or variant as a favourite; both calls are idempotent. */
export async function setFavorite(kind: FavoriteKind, id: string, on: boolean): Promise<void> {
	const api = createApi();
	const params = { params: { path: { id } } };
	if (kind === 'recipe') {
		await unwrap(
			on ? api.PUT('/recipes/{id}/favorite', params) : api.DELETE('/recipes/{id}/favorite', params)
		);
	} else {
		await unwrap(
			on
				? api.PUT('/variants/{id}/favorite', params)
				: api.DELETE('/variants/{id}/favorite', params)
		);
	}
}
