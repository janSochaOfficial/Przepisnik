import { error } from '@sveltejs/kit';
import { dev } from '$app/environment';
import type { PageLoad } from './$types';

/**
 * The UI showcase exists in `vite dev`, and in builds made with `VITE_UI_SHOWCASE=1` (the Playwright
 * integration build, which checks it for a11y). A normal production build answers 404.
 */
export const load: PageLoad = () => {
	if (!dev && import.meta.env.VITE_UI_SHOWCASE !== '1') error(404, 'Not found');
};
