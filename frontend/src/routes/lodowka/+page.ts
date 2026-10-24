import { redirect } from '@sveltejs/kit';
import { resolve } from '$app/paths';
import { createApi, unwrap } from '$lib/api/client';
import type { PageLoad } from './$types';

/** `/lodowka` opens the default fridge (or the oldest one); only a user without any fridge stays here. */
export const load: PageLoad = async ({ fetch, parent }) => {
	const { me } = await parent();
	const fridges = await unwrap(createApi({ fetch }).GET('/fridges'));
	const target = fridges.find((f) => f.id === me?.settings.defaultFridgeId) ?? fridges[0];
	if (target) redirect(307, resolve('/lodowka/[id]', { id: target.id }));
	return {};
};
