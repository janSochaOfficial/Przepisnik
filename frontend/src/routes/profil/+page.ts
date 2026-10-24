import { createApi, unwrap } from '$lib/api/client';
import type { PageLoad } from './$types';

/** The user's fridges, for the default-fridge select. A failure is shown on the page, not as an error screen. */
export const load: PageLoad = async ({ fetch, depends }) => {
	depends('app:profile');
	try {
		return { fridges: await unwrap(createApi({ fetch }).GET('/fridges')), fridgesError: null };
	} catch (error) {
		return { fridges: null, fridgesError: error };
	}
};
