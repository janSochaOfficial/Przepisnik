import createClient, { type Client } from 'openapi-fetch';
import type { components, paths } from './schema';

/** ProblemDetails body of every API error (TECHNICAL §3.6). */
export type Problem = components['schemas']['ProblemDetails'];

/** Header that every mutating request must carry (CSRF defence, TECHNICAL §3.5). */
export const CSRF_HEADER = { 'X-Requested-With': 'fetch' } as const;

/** Client-side code for requests that never reached the server (not part of API.md §9). */
export const NETWORK_ERROR_CODE = 'network.error';

/** A failed API call, with the stable `code` that the UI maps to Polish text via `t.errors`. */
export class ApiError extends Error {
	readonly status: number;
	readonly code: string;
	readonly errors: Record<string, string[]>;
	readonly problem: Problem | null;

	constructor(
		status: number,
		code: string,
		errors: Record<string, string[]> = {},
		problem: Problem | null = null
	) {
		super(`API error ${status} ${code}`);
		this.name = 'ApiError';
		this.status = status;
		this.code = code;
		this.errors = errors;
		this.problem = problem;
	}
}

/** Mirrors the backend fallback (`ProblemCodes.ForStatus`) for responses without a ProblemDetails body. */
export function fallbackCode(status: number): string {
	if (status === 0) return NETWORK_ERROR_CODE;
	if (status === 401) return 'auth.required';
	if (status === 403) return 'request.forbidden';
	if (status === 404) return 'request.not_found';
	if (status === 409) return 'concurrency.conflict';
	if (status === 429) return 'rate_limited';
	if (status >= 500) return 'server.error';
	return 'request.invalid';
}

function isRecord(value: unknown): value is Record<string, unknown> {
	return typeof value === 'object' && value !== null && !Array.isArray(value);
}

function readErrors(value: unknown): Record<string, string[]> {
	if (!isRecord(value)) return {};
	const out: Record<string, string[]> = {};
	for (const [path, codes] of Object.entries(value)) {
		if (Array.isArray(codes)) out[path] = codes.filter((c): c is string => typeof c === 'string');
	}
	return out;
}

/** Converts an error response (status + parsed body, if any) into an {@link ApiError}. */
export function toApiError(status: number, body: unknown): ApiError {
	if (isRecord(body) && typeof body.code === 'string' && body.code.length > 0) {
		return new ApiError(status, body.code, readErrors(body.errors), body as Problem);
	}
	return new ApiError(status, fallbackCode(status));
}

/** The login URL that brings the user back to `url` afterwards. */
export function loginUrl(url: URL): string {
	return `/logowanie?next=${encodeURIComponent(url.pathname + url.search)}`;
}

export type ApiOptions = {
	/** The `fetch` to use; pass SvelteKit's `fetch` inside `load` functions. */
	fetch?: (input: Request) => Promise<Response>;
	/** Called on any 401. Defaults to a redirect to `/logowanie?next=…`; `null` disables it. */
	onUnauthorized?: (() => void) | null;
};

export type Api = Client<paths>;

function redirectToLogin(): void {
	if (typeof window === 'undefined') return;
	const target = new URL(window.location.href);
	if (target.pathname === '/logowanie') return;
	void import('$app/navigation').then(({ goto }) => goto(loginUrl(target)));
}

/** Absolute in the browser (so `Request` never sees a relative URL), relative elsewhere. */
function apiBaseUrl(): string {
	return typeof window === 'undefined'
		? '/api/v1'
		: new URL('/api/v1', window.location.origin).href;
}

/** A typed client for `/api/v1` (types generated from `docs/api/openapi.yaml`). */
export function createApi(options: ApiOptions = {}): Api {
	const onUnauthorized =
		options.onUnauthorized === undefined ? redirectToLogin : options.onUnauthorized;
	const client = createClient<paths>({
		baseUrl: apiBaseUrl(),
		fetch: options.fetch ?? ((input: Request) => globalThis.fetch(input)),
		headers: CSRF_HEADER
	});
	client.use({
		onResponse({ response }) {
			if (response.status === 401 && onUnauthorized) onUnauthorized();
			return undefined;
		}
	});
	return client;
}

type CallResult<T> = { data?: T; error?: unknown; response: Response };

/**
 * Awaits an openapi-fetch call and returns its data, or throws an {@link ApiError}.
 * `204 No Content` resolves to `undefined`.
 */
export async function unwrap<T>(call: Promise<CallResult<T>>): Promise<T> {
	let result: CallResult<T>;
	try {
		result = await call;
	} catch (cause) {
		if (cause instanceof ApiError) throw cause;
		throw new ApiError(0, NETWORK_ERROR_CODE);
	}
	if (result.error !== undefined || !result.response.ok) {
		throw toApiError(result.response.status, result.error);
	}
	return result.data as T;
}
