import { createApi, unwrap } from '$lib/api/client';
import type { PageLoad } from './$types';

export const load: PageLoad = async ({ fetch, params }) => {
	const recipe = await unwrap(
		createApi({ fetch }).GET('/recipes/{id}', { params: { path: { id: params.id } } })
	);
	return { recipe };
};
