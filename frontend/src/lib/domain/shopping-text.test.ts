import { describe, expect, it } from 'vitest';
import { shoppingLine, shoppingListText } from './shopping-text';
import type { ProductRef, ShoppingListItem } from './types';

const product = (name: string, over: Partial<ProductRef> = {}): ProductRef =>
	({
		id: name,
		name,
		category: 'other',
		measureKind: 'volume',
		displayClass: 'liquid',
		hasSizes: false,
		allowHalf: false,
		pieceForms: null,
		...over
	}) as ProductRef;

const milk = product('mleko');
const flour = product('mąka', { measureKind: 'mass', displayClass: 'weight' });
const egg = product('jajko', {
	measureKind: 'count',
	displayClass: 'piece',
	pieceForms: { one: 'jajko', few: 'jajka', many: 'jajek', fraction: 'jajka' }
});

const item = (over: Partial<ShoppingListItem>): ShoppingListItem =>
	({
		id: 'x',
		version: 'v',
		product: null,
		text: null,
		amount: null,
		checked: false,
		...over
	}) as ShoppingListItem;

describe('shoppingLine', () => {
	it('capitalises the product and formats the amount in the inventory format', () => {
		expect(shoppingLine(item({ product: milk, amount: 1500 }), 'kitchen')).toBe('Mleko — 1,5 l');
		expect(shoppingLine(item({ product: flour, amount: 500 }), 'kitchen')).toBe('Mąka — 500 g');
	});

	it('uses metric for liquids whatever the user setting is', () => {
		expect(shoppingLine(item({ product: milk, amount: 250 }), 'kitchen')).toBe('Mleko — 250 ml');
	});

	it('applies Polish plural forms to pieces', () => {
		expect(shoppingLine(item({ product: egg, amount: 1 }), 'metric')).toBe('Jajko — 1 jajko');
		expect(shoppingLine(item({ product: egg, amount: 3 }), 'metric')).toBe('Jajko — 3 jajka');
		expect(shoppingLine(item({ product: egg, amount: 10 }), 'metric')).toBe('Jajko — 10 jajek');
	});

	it('shows only the name without an amount, and free text as typed', () => {
		expect(shoppingLine(item({ product: milk }), 'metric')).toBe('Mleko');
		expect(shoppingLine(item({ text: ' papier do pieczenia ' }), 'metric')).toBe(
			'papier do pieczenia'
		);
	});

	it('marks a rounded amount with ~', () => {
		expect(shoppingLine(item({ product: flour, amount: 103 }), 'metric')).toBe('Mąka — 100 g');
		expect(shoppingLine(item({ product: flour, amount: 7.4 }), 'metric')).toBe('Mąka — ~7 g');
	});
});

describe('shoppingListText', () => {
	it('is a title, a blank line and one unchecked item per line', () => {
		const text = shoppingListText(
			'Moja lodówka',
			[
				item({ product: milk, amount: 1500 }),
				item({ product: flour, amount: 500, checked: true }),
				item({ text: 'papier do pieczenia' }),
				item({ product: egg })
			],
			'kitchen'
		);
		expect(text).toBe('Lista zakupów — Moja lodówka\n\nMleko — 1,5 l\npapier do pieczenia\nJajko');
	});

	it('is only the title and a blank line for an empty list', () => {
		expect(shoppingListText('Dom', [], 'kitchen')).toBe('Lista zakupów — Dom\n');
	});
});
