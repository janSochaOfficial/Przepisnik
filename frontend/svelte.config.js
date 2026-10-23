import adapter from '@sveltejs/adapter-static';
import { vitePreprocess } from '@sveltejs/vite-plugin-svelte';

/** @type {import('@sveltejs/kit').Config} */
const config = {
	preprocess: vitePreprocess(),
	compilerOptions: {
		runes: true
	},
	kit: {
		// SPA (TECHNICAL §4.1): every route falls back to index.html, which Caddy serves.
		adapter: adapter({ fallback: 'index.html', strict: true }),
		alias: {
			$shared: '../shared'
		}
	}
};

export default config;
