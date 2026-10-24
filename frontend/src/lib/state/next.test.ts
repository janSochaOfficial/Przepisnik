import { describe, expect, it } from 'vitest';
import { safeNext } from './next';

describe('safeNext', () => {
	it.each([
		['/lodowka', '/lodowka'],
		['/zaproszenie/abc?x=1', '/zaproszenie/abc?x=1'],
		[null, '/przepisy'],
		['', '/przepisy'],
		['https://evil.example', '/przepisy'],
		['//evil.example', '/przepisy'],
		['/\\evil.example', '/przepisy']
	])('%s -> %s', (input, expected) => {
		expect(safeNext(input)).toBe(expected);
	});
});
