// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

// TECHNICAL §4.2 / CLAUDE.md: user-facing Polish text lives only in src/lib/i18n/pl/.
// Any Polish diacritic in application source outside that folder is reported, file:line.
const SRC = fileURLToPath(new URL('../../', import.meta.url));
const POLISH = /[ąćęłńóśźżĄĆĘŁŃÓŚŹŻ]/;
const SCANNED = /\.(ts|js|svelte|html)$/;

function isExempt(path: string): boolean {
	const rel = relative(SRC, path).split(sep).join('/');
	return (
		rel.startsWith('lib/i18n/pl/') ||
		rel === 'lib/api/schema.d.ts' ||
		/\.(test|spec)\.ts$/.test(rel)
	);
}

function walk(dir: string): string[] {
	return readdirSync(dir).flatMap((name) => {
		const path = join(dir, name);
		if (statSync(path).isDirectory()) return walk(path);
		return SCANNED.test(name) ? [path] : [];
	});
}

function findPolishLiterals(root: string): string[] {
	return walk(root)
		.filter((path) => !isExempt(path))
		.flatMap((path) =>
			readFileSync(path, 'utf8')
				.split('\n')
				.flatMap((line, i) =>
					POLISH.test(line) ? [`${relative(root, path)}:${i + 1}: ${line.trim()}`] : []
				)
		);
}

describe('i18n', () => {
	it('keeps Polish text out of source files outside src/lib/i18n/pl/', () => {
		expect(findPolishLiterals(SRC)).toEqual([]);
	});

	it('detects a Polish word in a quoted string', () => {
		expect(POLISH.test("const label = 'Lodówka';")).toBe(true);
		expect(POLISH.test("const label = 'Fridge';")).toBe(false);
	});
});
