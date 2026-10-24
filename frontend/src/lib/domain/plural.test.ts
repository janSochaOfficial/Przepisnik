import { describe, expect, it } from 'vitest';
import { pluralCategory, suggestPluralForms } from './plural';

describe('pluralCategory', () => {
	it.each([
		[1, 'one'],
		[2, 'few'],
		[3, 'few'],
		[4, 'few'],
		[5, 'many'],
		[0, 'many'],
		[10, 'many'],
		[11, 'many'],
		[12, 'many'],
		[13, 'many'],
		[14, 'many'],
		[15, 'many'],
		[21, 'many'],
		[22, 'few'],
		[24, 'few'],
		[25, 'many'],
		[100, 'many'],
		[101, 'many'],
		[102, 'few'],
		[112, 'many'],
		[114, 'many'],
		[122, 'few']
	] as const)('%d is %s', (n, expected) => {
		expect(pluralCategory(n)).toBe(expected);
	});

	it.each([0.5, 1.5, 2.25, 0.75])('%d has the fraction form', (n) => {
		expect(pluralCategory(n)).toBe('fraction');
	});
});

describe('suggestPluralForms', () => {
	it.each([
		['puszka', ['puszka', 'puszki', 'puszek', 'puszki']],
		['filiżanka', ['filiżanka', 'filiżanki', 'filiżanek', 'filiżanki']],
		['kostka', ['kostka', 'kostki', 'kostek', 'kostki']],
		['szczypta', ['szczypta', 'szczypty', 'szczypt', 'szczypty']],
		['porcja', ['porcja', 'porcje', 'porcji', 'porcji']],
		['pęczek', ['pęczek', 'pęczki', 'pęczków', 'pęczka']],
		['garść', ['garść', 'garści', 'garści', 'garści']],
		['słoik', ['słoik', 'słoiki', 'słoików', 'słoika']],
		['  puszka   duża ', ['puszka duża', 'puszki duża', 'puszek duża', 'puszki duża']],
		['xyz', ['xyz', 'xyz', 'xyz', 'xyz']]
	])('guesses the forms of %s', (name, [one, few, many, fraction]) => {
		expect(suggestPluralForms(name)).toEqual({ one, few, many, fraction });
	});
});
