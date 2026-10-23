// Runs the Svelte autofixer (svelte-code-writer skill) on the given .svelte / .svelte.ts files, or on all of
// them under src/ when no file is given, and fails when any file has issues.
//   node scripts/svelte-autofix.mjs [files…]
import { execFileSync } from 'node:child_process';
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

const MCP = '@sveltejs/mcp@0.1.27';
const svelteVersion = JSON.parse(readFileSync('package.json', 'utf8')).devDependencies.svelte;

function walk(dir) {
	return readdirSync(dir).flatMap((name) => {
		const path = join(dir, name);
		if (statSync(path).isDirectory()) return walk(path);
		return /\.svelte(\.[jt]s)?$/.test(name) ? [path] : [];
	});
}

const files = process.argv.length > 2 ? process.argv.slice(2) : walk('src').sort();
let failed = 0;
for (const file of files) {
	const out = execFileSync(
		'npx',
		['-y', MCP, 'svelte-autofixer', file, '--svelte-version', svelteVersion],
		{ encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] }
	);
	const clean = /issues: \[\]/.test(out) && /suggestions: \[\]/.test(out);
	console.log(`${clean ? 'ok  ' : 'FAIL'} ${file}`);
	if (!clean) {
		failed++;
		console.log(out);
	}
}
if (failed > 0) {
	console.error(`svelte-autofixer: ${failed} file(s) with issues`);
	process.exit(1);
}
