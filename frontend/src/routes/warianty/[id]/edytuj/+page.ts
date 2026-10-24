import { ApiError, createApi, unwrap } from '$lib/api/client';
import type { PageLoad } from './$types';

export const load: PageLoad = async ({ fetch, params }) => {
	const api = createApi({ fetch });
	const path = { id: params.id };
	const variant = await unwrap(api.GET('/variants/{id}', { params: { path } }));
	if (!variant.baseAvailable) return { variant, base: null, resolved: null };
	try {
		const [base, resolved] = await Promise.all([
			unwrap(api.GET('/recipes/{id}', { params: { path: { id: variant.recipeId } } })),
			unwrap(api.GET('/variants/{id}/resolved', { params: { path } }))
		]);
		return { variant, base, resolved };
	} catch (error) {
		// The base disappeared after the list was loaded: treat it like `baseAvailable: false`.
		if (error instanceof ApiError && error.status === 404)
			return { variant, base: null, resolved: null };
		throw error;
	}
};
