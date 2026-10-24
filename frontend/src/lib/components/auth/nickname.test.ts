import { describe, expect, it } from 'vitest';
import { cleanNickname, nicknameProblem } from './nickname';

describe('nicknameProblem', () => {
	it.each([
		['ania', null],
		['  Zażółć_gęślą-1  ', null],
		['abc', null],
		['a'.repeat(30), null],
		['', 'empty'],
		['   ', 'empty'],
		['ab', 'tooShort'],
		['a'.repeat(31), 'tooLong'],
		['ania kowalska', 'badChars'],
		['ania!', 'badChars'],
		['ania@x', 'badChars']
	])('%j -> %s', (input, expected) => {
		expect(nicknameProblem(input)).toBe(expected);
	});

	it('composes a decomposed letter before counting and matching', () => {
		expect(cleanNickname('záb')).toBe('záb');
		expect(nicknameProblem('záb')).toBeNull();
		expect(nicknameProblem('ąb')).toBe('tooShort'); // "ąb" is two characters
	});
});
