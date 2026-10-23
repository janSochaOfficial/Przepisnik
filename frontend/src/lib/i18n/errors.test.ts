// @vitest-environment node
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { errorMessage, errors } from './pl/errors';

const apiMd = readFileSync(
	fileURLToPath(new URL('../../../../docs/API.md', import.meta.url)),
	'utf8'
);

/** Every backticked code in the "## 9. Error codes" section of API.md. */
function documentedCodes(markdown: string): string[] {
	const start = markdown.indexOf('## 9. Error codes');
	if (start < 0) throw new Error('API.md has no "## 9. Error codes" section');
	const rest = markdown.slice(start + 1);
	const end = rest.search(/\n## /);
	const section = end < 0 ? rest : rest.slice(0, end);
	return [...section.matchAll(/`([a-z_]+(?:\.[a-z_]+)*)`/g)].map((m) => m[1] as string);
}

describe('errors', () => {
	const codes = documentedCodes(apiMd);

	it('finds the documented codes in API.md', () => {
		expect(codes.length).toBeGreaterThan(40);
		expect(codes).toContain('recipe.not_found');
	});

	it('has a Polish message for every code in API.md §9', () => {
		const missing = codes.filter((code) => !(code in errors));
		expect(missing).toEqual([]);
	});

	it('has no empty messages', () => {
		for (const [code, message] of Object.entries(errors)) {
			expect(message.trim(), code).not.toBe('');
		}
	});

	it('falls back to the generic message for unknown codes', () => {
		expect(errorMessage('nope.unknown')).toBe(errors['server.error']);
		expect(errorMessage('nickname.taken')).toBe(errors['nickname.taken']);
	});
});
