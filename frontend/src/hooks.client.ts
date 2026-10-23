import type { HandleClientError } from '@sveltejs/kit';
import { ApiError } from '$lib/api/client';

/** Keeps the API error code so that +error.svelte can show the Polish message for it. */
export const handleError: HandleClientError = ({ error, status }) => ({
	message: error instanceof Error ? error.message : 'Unexpected error',
	code:
		error instanceof ApiError ? error.code : status === 404 ? 'request.not_found' : 'server.error'
});
