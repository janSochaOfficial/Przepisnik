import { afterEach, describe, expect, it, vi } from 'vitest';

const environment = vi.hoisted(() => ({ dev: false }));
vi.mock('$app/environment', () => environment);

async function load() {
	const { load } = await import('./+page');
	return () => (load as () => unknown)();
}

describe('showcase guard', () => {
	afterEach(() => {
		vi.unstubAllEnvs();
		environment.dev = false;
	});

	it('answers 404 in a production build without the flag', async () => {
		vi.stubEnv('VITE_UI_SHOWCASE', '');
		const run = await load();

		expect(run).toThrow(expect.objectContaining({ status: 404 }));
	});

	it('renders in dev', async () => {
		environment.dev = true;
		const run = await load();

		expect(run).not.toThrow();
	});

	it('renders in a build made with VITE_UI_SHOWCASE=1', async () => {
		vi.stubEnv('VITE_UI_SHOWCASE', '1');
		const run = await load();

		expect(run).not.toThrow();
	});
});
