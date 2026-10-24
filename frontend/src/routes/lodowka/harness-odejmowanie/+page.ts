import { error } from '@sveltejs/kit';
import { dev } from '$app/environment';
import type { PageLoad } from './$types';

/**
 * Test harness for the deduction dialog (same gate as the /ui showcase): available in `vite dev`
 * and in builds made with `VITE_UI_SHOWCASE=1`, 404 otherwise. The recipe view hosts the real
 * dialog (covered by tests/e2e); this page keeps the dialog's edge cases testable with small,
 * fixed fixtures in tests/integration/fridge-shopping.spec.ts.
 */
export const load: PageLoad = () => {
	if (!dev && import.meta.env.VITE_UI_SHOWCASE !== '1') error(404, 'Not found');
};
