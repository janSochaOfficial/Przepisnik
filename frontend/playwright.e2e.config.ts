import { defineConfig, devices } from '@playwright/test';

// End-to-end smoke (TECHNICAL §6): the real stack from deploy/compose.yaml + deploy/compose.e2e.yaml
// (dev-login on). Start it first, or let `scripts/verify.sh --e2e` do the whole cycle.
const baseURL = process.env.E2E_BASE_URL || 'http://localhost:8080';

export default defineConfig({
	testDir: 'tests/e2e',
	outputDir: 'test-results/e2e',
	fullyParallel: false,
	workers: 1,
	forbidOnly: !!process.env.CI,
	retries: 0,
	timeout: 300_000,
	expect: { timeout: 15_000 },
	reporter: process.env.CI ? 'github' : 'list',
	use: {
		baseURL,
		locale: 'pl-PL',
		timezoneId: 'Europe/Warsaw',
		trace: 'retain-on-failure',
		screenshot: 'only-on-failure'
	},
	projects: [
		{
			name: 'desktop',
			use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 900 } }
		}
	]
});
