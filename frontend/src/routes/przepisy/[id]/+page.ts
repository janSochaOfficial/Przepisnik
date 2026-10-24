import { createApi, unwrap } from '$lib/api/client';
import type { PageLoad } from './$types';

export const load: PageLoad = async ({ fetch, params, depends, url }) => {
	depends(`app:recipe:${params.id}`);
	const api = createApi({ fetch });
	const path = { id: params.id };
	const [recipe, matrix, selection, preferences] = await Promise.all([
		unwrap(api.GET('/recipes/{id}', { params: { path } })),
		unwrap(api.GET('/recipes/{id}/variant-matrix', { params: { path } })),
		unwrap(api.GET('/recipes/{id}/my-selection', { params: { path } })),
		unwrap(api.GET('/me/product-preferences'))
	]);
	return {
		recipe,
		matrix,
		// 204 (nothing saved yet) resolves to undefined.
		selection: selection ?? null,
		preferences,
		pinVariantId: url.searchParams.get('wariant')
	};
};
