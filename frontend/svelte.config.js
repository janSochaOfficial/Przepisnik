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
		// The SPA shell has an inline bootstrap script, so the policy lives here: SvelteKit hashes that script into a
		// <meta> CSP. Caddy adds the headers a <meta> cannot carry (frame-ancestors via X-Frame-Options, HSTS, …).
		csp: {
			mode: 'hash',
			directives: {
				'default-src': ['self'],
				'script-src': ['self', 'https://accounts.google.com/gsi/client'],
				'style-src': ['self', 'unsafe-inline', 'https://accounts.google.com/gsi/style'],
				'img-src': ['self', 'data:', 'blob:', 'https://*.googleusercontent.com'],
				'font-src': ['self'],
				'connect-src': ['self', 'https://accounts.google.com/gsi/'],
				'frame-src': ['https://accounts.google.com/gsi/'],
				'object-src': ['none'],
				'base-uri': ['self'],
				'form-action': ['self']
			}
		},
		alias: {
			$shared: '../shared'
		}
	}
};

export default config;
