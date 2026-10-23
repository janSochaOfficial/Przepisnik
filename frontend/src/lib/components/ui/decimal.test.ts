import { describe, expect, it } from 'vitest';
import { formatDecimal, parseDecimal } from './decimal';

describe('parseDecimal', () => {
	it.each([
		['1,5', 1.5],
		['1.5', 1.5],
		[',5', 0.5],
		['2,', 2],
		['250', 250],
		['1 250,5', 1250.5],
		['1\u202f250', 1250],
		['-3,25', -3.25],
		[' 7 ', 7]
	])('reads %j as %d', (text, value) => {
		expect(parseDecimal(text)).toEqual({ kind: 'number', value });
	});

	it.each(['abc', '1,2,3', '1.2.3', '1e3', '--1', ',', '12a', '0x10'])('rejects %j', (text) => {
		expect(parseDecimal(text)).toEqual({ kind: 'invalid' });
	});

	it('treats blank text as empty', () => {
		expect(parseDecimal('')).toEqual({ kind: 'empty' });
		expect(parseDecimal('   ')).toEqual({ kind: 'empty' });
	});
});

describe('formatDecimal', () => {
	it('prints a decimal comma without grouping', () => {
		expect(formatDecimal(1.5)).toBe('1,5');
		expect(formatDecimal(1250)).toBe('1250');
		expect(formatDecimal(0.125)).toBe('0,125');
	});

	it('prints nothing for a missing value', () => {
		expect(formatDecimal(null)).toBe('');
		expect(formatDecimal(undefined)).toBe('');
		expect(formatDecimal(Number.NaN)).toBe('');
	});
});
