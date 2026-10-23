import { sveltekit } from '@sveltejs/kit/vite';
import tailwindcss from '@tailwindcss/vite';
import { svelteTesting } from '@testing-library/svelte/vite';
import { defineConfig } from 'vitest/config';
import { loadPorts } from './config/ports.ts';

const { apiPort, webPort, previewPort } = loadPorts();
const proxy = { '/api': { target: `http://localhost:${apiPort}`, changeOrigin: false } };

export default defineConfig({
	plugins: [tailwindcss(), sveltekit()],
	server: { port: webPort, strictPort: true, proxy, fs: { allow: ['..'] } },
	preview: { port: previewPort, strictPort: true, proxy },
	test: {
		projects: [
			{
				extends: true,
				plugins: [svelteTesting()],
				test: {
					name: 'unit',
					environment: 'jsdom',
					include: ['src/**/*.{test,spec}.ts', 'config/**/*.test.ts', 'scripts/**/*.test.ts'],
					setupFiles: ['./vitest-setup.ts'],
					restoreMocks: true
				}
			}
		],
		coverage: {
			provider: 'v8',
			include: ['src/lib/**/*.ts'],
			exclude: ['src/lib/api/schema.d.ts', 'src/lib/i18n/**', '**/*.test.ts']
		}
	}
});
