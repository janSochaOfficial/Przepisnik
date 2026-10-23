import { redirect } from '@sveltejs/kit';
import { ApiError, createApi, loginUrl, unwrap } from '$lib/api/client';
import type { Me } from '$lib/domain/types';
import type { LayoutLoad } from './$types';

// SPA (TECHNICAL §4.1): the whole app is behind login, so there is no SSR and no prerendering.
export const ssr = false;
export const prerender = false;

/** Routes reachable without a session. */
const PUBLIC_ROUTES = new Set(['/logowanie']);
const ONBOARDING_ROUTE = '/witaj';

/**
 * Auth guard: `GET /me` → 401 sends the user to `/logowanie?next=…`; a user without a nickname is sent
 * to `/witaj` (onboarding) until they pick one.
 */
export const load: LayoutLoad = async ({ fetch, url, depends }) => {
	depends('app:me');
	if (PUBLIC_ROUTES.has(url.pathname)) return { me: null };

	let me: Me;
	try {
		me = await unwrap(createApi({ fetch, onUnauthorized: null }).GET('/me'));
	} catch (error) {
		if (error instanceof ApiError && error.status === 401) redirect(307, loginUrl(url));
		throw error;
	}

	if (me.nickname === null && url.pathname !== ONBOARDING_ROUTE) redirect(307, ONBOARDING_ROUTE);
	return { me };
};
