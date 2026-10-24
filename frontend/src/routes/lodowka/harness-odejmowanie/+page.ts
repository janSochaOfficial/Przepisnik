import { error } from '@sveltejs/kit';
import { dev } from '$app/environment';
import type { PageLoad } from './$types';

/**
 * Test harness for the deduction dialog until the recipe view hosts it (same gate as the /ui
 * showcase): available in `vite dev` and in builds made with `VITE_UI_SHOWCASE=1`, 404 otherwise.
 */
export const load: PageLoad = () => {
	if (!dev && import.meta.env.VITE_UI_SHOWCASE !== '1') error(404, 'Not found');
};
