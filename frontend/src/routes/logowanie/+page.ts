import { createApi, unwrap } from '$lib/api/client';
import type { PageLoad } from './$types';

/** Only same-app paths are allowed as the post-login target (no open redirects). */
function safeNext(next: string | null): string {
	return next && next.startsWith('/') && !next.startsWith('//') ? next : '/przepisy';
}

export const load: PageLoad = async ({ fetch, url }) => {
	const config = await unwrap(createApi({ fetch, onUnauthorized: null }).GET('/auth/config'));
	return { config, next: safeNext(url.searchParams.get('next')) };
};
