// Mocked API for Playwright integration tests (TECHNICAL §6). Additive only (plan §4.2);
// fixtures live in one file per feature next to this one.
import type { Page, Request } from '@playwright/test';

export type MockReply = {
	status?: number;
	body?: unknown;
	headers?: Record<string, string>;
};

export type MockContext = {
	request: Request;
	/** Path parameters from the handler key, e.g. `{ id }` for `GET /recipes/:id`. */
	params: Record<string, string>;
	url: URL;
	/** The parsed JSON request body, if any. */
	json: unknown;
};

/** A canned reply, or a function computing one from the request. */
export type MockHandler = MockReply | ((ctx: MockContext) => MockReply | Promise<MockReply>);

/** Keys are `"<METHOD> <path under /api/v1>"`, with `:name` path parameters, e.g. `"GET /recipes/:id"`. */
export type MockHandlers = Record<string, MockHandler>;

export type MockedApi = {
	/** Every request that reached the mock, in order, as `"<METHOD> <path>"`. */
	calls: { key: string; json: unknown }[];
};

function match(pattern: string, path: string): Record<string, string> | null {
	const a = pattern.split('/').filter(Boolean);
	const b = path.split('/').filter(Boolean);
	if (a.length !== b.length) return null;
	const params: Record<string, string> = {};
	for (let i = 0; i < a.length; i++) {
		const p = a[i] as string;
		const v = b[i] as string;
		if (p.startsWith(':')) params[p.slice(1)] = decodeURIComponent(v);
		else if (p !== v) return null;
	}
	return params;
}

/**
 * Routes every `/api/v1/**` request of `page` to `handlers`. Unmatched requests answer
 * 501 with `code: "server.error"` and are listed in the returned `calls` so tests can assert on them.
 */
export async function mockApi(page: Page, handlers: MockHandlers): Promise<MockedApi> {
	const api: MockedApi = { calls: [] };
	const entries = Object.entries(handlers).map(([key, handler]) => {
		const [method, pattern] = key.split(' ', 2) as [string, string];
		return { method: method.toUpperCase(), pattern, handler };
	});

	await page.route('**/api/v1/**', async (route) => {
		const request = route.request();
		const url = new URL(request.url());
		const path = url.pathname.replace(/^\/api\/v1/, '');
		let json: unknown;
		try {
			json = request.postDataJSON();
		} catch {
			json = request.postData();
		}
		api.calls.push({ key: `${request.method()} ${path}`, json });

		for (const entry of entries) {
			if (entry.method !== request.method()) continue;
			const params = match(entry.pattern, path);
			if (!params) continue;
			const reply =
				typeof entry.handler === 'function'
					? await entry.handler({ request, params, url, json })
					: entry.handler;
			const status = reply.status ?? 200;
			const isProblem = status >= 400;
			await route.fulfill({
				status,
				headers: reply.headers,
				contentType: isProblem ? 'application/problem+json' : 'application/json',
				body: reply.body === undefined ? '' : JSON.stringify(reply.body)
			});
			return;
		}

		await route.fulfill({
			status: 501,
			contentType: 'application/problem+json',
			body: JSON.stringify({
				status: 501,
				code: 'server.error',
				detail: `No mock for ${request.method()} ${path}`
			})
		});
	});

	return api;
}

/** A ProblemDetails reply (API errors always carry a `code`). */
export function problem(
	status: number,
	code: string,
	extra: Record<string, unknown> = {}
): MockReply {
	return { status, body: { status, code, ...extra } };
}
