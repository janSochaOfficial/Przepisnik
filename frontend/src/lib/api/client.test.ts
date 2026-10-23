import { describe, expect, it, vi } from 'vitest';
import {
	ApiError,
	CSRF_HEADER,
	createApi,
	fallbackCode,
	loginUrl,
	toApiError,
	unwrap
} from './client';

function jsonResponse(status: number, body: unknown, contentType = 'application/json'): Response {
	return new Response(body === undefined ? null : JSON.stringify(body), {
		status,
		headers: { 'Content-Type': contentType }
	});
}

describe('toApiError', () => {
	it('keeps the code, the validation errors and the problem of a ProblemDetails body', () => {
		const body = {
			status: 400,
			code: 'quantity.unit_incompatible',
			errors: { 'ingredients[3].quantity': ['quantity.unit_incompatible'] }
		};
		const error = toApiError(400, body);
		expect(error).toBeInstanceOf(ApiError);
		expect(error.status).toBe(400);
		expect(error.code).toBe('quantity.unit_incompatible');
		expect(error.errors).toEqual({ 'ingredients[3].quantity': ['quantity.unit_incompatible'] });
		expect(error.problem).toBe(body);
	});

	it('ignores malformed errors entries', () => {
		const error = toApiError(400, { code: 'validation.failed', errors: { a: 'x', b: ['ok', 3] } });
		expect(error.errors).toEqual({ b: ['ok'] });
	});

	it.each([
		[401, 'auth.required'],
		[403, 'request.forbidden'],
		[404, 'request.not_found'],
		[409, 'concurrency.conflict'],
		[413, 'request.invalid'],
		[429, 'rate_limited'],
		[500, 'server.error'],
		[502, 'server.error'],
		[0, 'network.error']
	])('falls back to a code by status %i when the body has none', (status, code) => {
		expect(toApiError(status, undefined).code).toBe(code);
		expect(toApiError(status, 'Bad Gateway').code).toBe(code);
		expect(fallbackCode(status)).toBe(code);
	});
});

describe('loginUrl', () => {
	it('encodes the current path and query as next', () => {
		expect(loginUrl(new URL('http://localhost/przepisy/1?wariant=2'))).toBe(
			'/logowanie?next=%2Fprzepisy%2F1%3Fwariant%3D2'
		);
	});
});

describe('createApi + unwrap', () => {
	it('sends the CSRF header and returns the data', async () => {
		const fetchMock = vi.fn(async (_request: Request) =>
			jsonResponse(200, { googleClientId: null, devLoginEnabled: true })
		);
		const api = createApi({ fetch: fetchMock, onUnauthorized: null });

		const config = await unwrap(api.GET('/auth/config'));

		expect(config).toEqual({ googleClientId: null, devLoginEnabled: true });
		const request = fetchMock.mock.calls[0]?.[0];
		expect(request?.url).toMatch(/\/api\/v1\/auth\/config$/);
		expect(request?.headers.get('X-Requested-With')).toBe(CSRF_HEADER['X-Requested-With']);
	});

	it('throws an ApiError with the problem code', async () => {
		const api = createApi({
			fetch: async () =>
				jsonResponse(409, { status: 409, code: 'nickname.taken' }, 'application/problem+json'),
			onUnauthorized: null
		});

		await expect(
			unwrap(api.PUT('/me/nickname', { body: { nickname: 'ania' } }))
		).rejects.toMatchObject({ status: 409, code: 'nickname.taken' });
	});

	it('calls onUnauthorized on 401', async () => {
		const onUnauthorized = vi.fn();
		const api = createApi({
			fetch: async () => jsonResponse(401, { status: 401, code: 'auth.required' }),
			onUnauthorized
		});

		await expect(unwrap(api.GET('/me'))).rejects.toMatchObject({
			status: 401,
			code: 'auth.required'
		});
		expect(onUnauthorized).toHaveBeenCalledOnce();
	});

	it('resolves 204 to undefined', async () => {
		const api = createApi({
			fetch: async () => new Response(null, { status: 204 }),
			onUnauthorized: null
		});
		await expect(unwrap(api.POST('/auth/logout'))).resolves.toBeUndefined();
	});

	it('maps a network failure to network.error', async () => {
		const api = createApi({
			fetch: async () => {
				throw new TypeError('Failed to fetch');
			},
			onUnauthorized: null
		});
		await expect(unwrap(api.GET('/me'))).rejects.toMatchObject({
			status: 0,
			code: 'network.error'
		});
	});
});
