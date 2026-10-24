import { describe, expect, it } from 'vitest';
import { canMove, dropRow, moveRow } from './lists';

const row = (id: string, base: boolean) => ({ id, base });
const ids = (rows: { id: string }[]) => rows.map((r) => r.id).join('');

describe('canMove', () => {
	const rows = [row('a', true), row('b', true), row('n', false)];

	it('refuses to swap two base rows', () => {
		expect(canMove(rows, 0, 'down')).toBe(false);
		expect(canMove(rows, 1, 'up')).toBe(false);
	});

	it('lets a new row pass a base row', () => {
		expect(canMove(rows, 2, 'up')).toBe(true);
		expect(canMove(rows, 1, 'down')).toBe(true);
	});

	it('refuses to leave the list', () => {
		expect(canMove(rows, 0, 'up')).toBe(false);
		expect(canMove(rows, 2, 'down')).toBe(false);
	});
});

describe('moveRow', () => {
	it('moves a row of a plain recipe', () => {
		expect(ids(moveRow([row('a', false), row('b', false)], 0, 'down'))).toBe('ba');
	});

	it('keeps base order in a variant', () => {
		const rows = [row('a', true), row('b', true), row('n', false)];
		expect(ids(moveRow(rows, 0, 'down'))).toBe('abn');
		expect(ids(moveRow(rows, 2, 'up'))).toBe('anb');
	});
});

describe('dropRow', () => {
	it('moves a new row anywhere between base rows', () => {
		const rows = [row('a', true), row('b', true), row('n', false)];
		expect(ids(dropRow(rows, 2, 0))).toBe('nab');
	});

	it('refuses a drop that reorders base rows', () => {
		const rows = [row('a', true), row('b', true)];
		expect(ids(dropRow(rows, 1, 0))).toBe('ab');
	});
});
