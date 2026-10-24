import { createApi, unwrap } from '$lib/api/client';
import { safeNext } from '$lib/state/next';
import type { PageLoad } from './$types';

export const load: PageLoad = async ({ fetch, url }) => {
	const config = await unwrap(createApi({ fetch, onUnauthorized: null }).GET('/auth/config'));
	return { config, next: safeNext(url.searchParams.get('next')) };
};
