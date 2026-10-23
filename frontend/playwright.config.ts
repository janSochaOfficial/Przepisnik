import { defineConfig, devices } from '@playwright/test';
import { loadPorts } from './config/ports.ts';

// Integration tests: the real built app (vite preview) with a mocked API (TECHNICAL §6).
const { previewPort } = loadPorts();
const baseURL = `http://localhost:${previewPort}`;

export default defineConfig({
	testDir: 'tests/integration',
	fullyParallel: true,
	forbidOnly: !!process.env.CI,
	retries: process.env.CI ? 1 : 0,
	reporter: process.env.CI ? 'github' : 'list',
	use: {
		baseURL,
		locale: 'pl-PL',
		timezoneId: 'Europe/Warsaw',
		trace: 'retain-on-failure'
	},
	projects: [
		{ name: 'mobile', use: { ...devices['Pixel 7'] } },
		{
			name: 'desktop',
			use: { ...devices['Desktop Chrome'], viewport: { width: 1280, height: 800 } }
		}
	],
	webServer: {
		command: `npm run build && npm run preview -- --port ${previewPort} --strictPort`,
		url: baseURL,
		reuseExistingServer: false,
		timeout: 180_000,
		// VITE_UI_SHOWCASE builds the dev-only /ui showcase (F0) so its a11y test can run.
		env: { PREVIEW_PORT: String(previewPort), VITE_UI_SHOWCASE: '1' }
	}
});
