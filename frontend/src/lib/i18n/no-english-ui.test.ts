// @vitest-environment node
import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

// CLAUDE.md: no English text in the UI. Every visible string comes from t.* (src/lib/i18n/pl/), so the
// markup of a component may hold only expressions, never literal words: neither text between tags
// nor literal values of the attributes a user sees or hears.
const SRC = fileURLToPath(new URL('../../', import.meta.url));
const VISIBLE_ATTRIBUTES = [
	'aria-label',
	'title',
	'placeholder',
	'alt',
	'label',
	'aria-description'
];

function walk(dir: string): string[] {
	return readdirSync(dir).flatMap((name) => {
		const path = join(dir, name);
		if (statSync(path).isDirectory()) return walk(path);
		// Test-only components (*.test.svelte) are not part of the UI.
		return name.endsWith('.svelte') && !name.endsWith('.test.svelte') ? [path] : [];
	});
}

/** The markup of a component: scripts, styles, comments and `{…}` expressions blanked out (lines kept). */
export function markupOf(source: string): string {
	const blank = (s: string) => s.replace(/[^\n]/g, ' ');
	let out = source
		.replace(/<script[\s\S]*?<\/script>/g, blank)
		.replace(/<style[\s\S]*?<\/style>/g, blank)
		.replace(/<!--[\s\S]*?-->/g, blank);
	// Expressions can nest braces ({#each items as { id }}); blank them innermost first.
	for (let previous = ''; previous !== out;) {
		previous = out;
		out = out.replace(/\{[^{}]*\}/g, blank);
	}
	return out;
}

/** Literal words in text nodes or visible attributes, as `line: text`. */
export function findLiteralText(source: string): string[] {
	const markup = markupOf(source);
	const found: string[] = [];
	const lineOf = (index: number) => markup.slice(0, index).split('\n').length;

	for (const match of markup.matchAll(/>([^<>]+)</g)) {
		const text = match[1]!.trim();
		if (/\p{L}{2,}/u.test(text)) found.push(`${lineOf(match.index)}: ${text}`);
	}
	const attributes = new RegExp(`\\s(${VISIBLE_ATTRIBUTES.join('|')})="([^"]*)"`, 'g');
	for (const match of markup.matchAll(attributes)) {
		if (/\p{L}{2,}/u.test(match[2]!))
			found.push(`${lineOf(match.index)}: ${match[1]}="${match[2]}"`);
	}
	return found;
}

describe('no English in the UI', () => {
	it('keeps literal words out of component markup (all texts come from t.*)', () => {
		const violations = walk(SRC).flatMap((path) =>
			findLiteralText(readFileSync(path, 'utf8')).map((v) => `${relative(SRC, path)}:${v}`)
		);
		expect(violations).toEqual([]);
	});

	it('flags a literal word between tags and in a visible attribute', () => {
		expect(findLiteralText('<button>Save</button>')).toEqual(['1: Save']);
		expect(findLiteralText('<img alt="Cover photo" src={x} />')).toEqual(['1: alt="Cover photo"']);
	});

	it('ignores expressions, scripts, styles and comments', () => {
		const source = [
			'<script>const a = "Hello";</script>',
			'<!-- English comment -->',
			'<p class="text-sm">{t.common.save} {count}</p>',
			'{#each items as { id } (id)}<span>{id}</span>{/each}',
			'<style>p { color: red; }</style>'
		].join('\n');
		expect(findLiteralText(source)).toEqual([]);
	});
});
