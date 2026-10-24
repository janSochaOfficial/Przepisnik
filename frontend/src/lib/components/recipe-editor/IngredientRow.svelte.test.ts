import '$lib/components/ui/testing';
import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import type { CustomUnitRef, ProductRef } from '$lib/domain/types';
import IngredientRowHarness from './IngredientRowHarness.test.svelte';

const catalog = vi.hoisted(() => ({ units: [] as CustomUnitRef[] }));
vi.mock('$lib/components/units/units-client', () => ({
	loadUnits: () => Promise.resolve(catalog.units),
	createUnit: vi.fn(),
	rememberUnit: vi.fn()
}));

const NEW = '+ Nowa jednostka…';
const forms = { one: 'puszka', few: 'puszki', many: 'puszek', fraction: 'puszki' };
const can: CustomUnitRef = {
	id: 'can',
	name: 'puszka',
	forms,
	productId: 'p',
	measureKind: 'mass',
	amount: 400
};
const otherCan: CustomUnitRef = { ...can, id: 'other-can', name: 'słoik', productId: 'other' };
const cup: CustomUnitRef = {
	...can,
	id: 'cup',
	name: 'filiżanka',
	productId: null,
	measureKind: 'volume',
	amount: 150
};

beforeEach(() => {
	catalog.units = [];
});

const base: ProductRef = {
	id: 'p',
	name: 'produkt',
	category: 'other',
	measureKind: 'mass',
	displayClass: 'weight',
	hasSizes: false,
	allowHalf: false,
	gramsPerMl: null,
	gramsPerPiece: null,
	defaultPackageAmount: null,
	pieceForms: null,
	isVerified: true
};

async function unitOptions(): Promise<string[]> {
	const user = userEvent.setup();
	await user.click(screen.getByRole('combobox', { name: 'Jednostka' }));
	return screen
		.getAllByRole('option', { hidden: true })
		.map((o) => o.textContent?.replace(/\s+/g, ' ').trim() ?? '');
}

describe('ingredient unit select', () => {
	it('offers only g and kg for a mass product without density', async () => {
		render(IngredientRowHarness, { product: base, unit: 'g' });
		expect(await unitOptions()).toEqual(['g', 'kg', NEW]);
	});

	it('offers volume and mass units for a liquid with density', async () => {
		render(IngredientRowHarness, {
			product: { ...base, measureKind: 'volume', displayClass: 'liquid', gramsPerMl: 1.03 },
			unit: 'ml'
		});
		const options = await unitOptions();
		expect(options).toContain('szklanka');
		expect(options).toContain('g');
		expect(options).not.toContain('szt.');
	});

	it('offers only pieces for a countable product, with a size select', async () => {
		render(IngredientRowHarness, {
			product: { ...base, measureKind: 'count', displayClass: 'piece', hasSizes: true },
			unit: 'piece'
		});
		expect(await unitOptions()).toEqual(['szt.', NEW]);
		expect(screen.getByRole('combobox', { name: 'Wielkość' })).toBeInTheDocument();
	});

	it('hides the amount for "do smaku"', async () => {
		const user = userEvent.setup();
		render(IngredientRowHarness, { product: base, unit: 'g' });
		await user.click(screen.getByRole('switch', { name: /Do smaku/ }));
		expect(screen.queryByLabelText('Ilość')).not.toBeInTheDocument();
	});
});

describe('custom units in the unit select', () => {
	it('lists the product units, then units for every product that convert, then the new entry', async () => {
		catalog.units = [can, otherCan, cup];
		render(IngredientRowHarness, { product: { ...base, gramsPerMl: 0.53 }, unit: 'g' });
		await vi.waitFor(async () => {
			const options = await unitOptions();
			expect(options.slice(-3)).toEqual(['puszka tylko dla tego produktu', 'filiżanka', NEW]);
		});
	});

	it('does not offer a volume unit for a mass product without density', async () => {
		catalog.units = [can, cup];
		render(IngredientRowHarness, { product: base, unit: 'g' });
		await vi.waitFor(async () => {
			expect(await unitOptions()).toEqual(['g', 'kg', 'puszka tylko dla tego produktu', NEW]);
		});
	});

	it('shows a chosen custom unit', async () => {
		render(IngredientRowHarness, { product: base, unit: 'custom', customUnit: can });
		expect(screen.getByRole('combobox', { name: 'Jednostka' })).toHaveTextContent('puszka');
	});

	it('opens the new-unit dialog with forms guessed from the name', async () => {
		const user = userEvent.setup();
		render(IngredientRowHarness, { product: base, unit: 'g' });
		await user.click(screen.getByRole('combobox', { name: 'Jednostka' }));
		await user.click(screen.getByRole('option', { name: NEW }));

		const dialog = await screen.findByRole('dialog', { name: 'Nowa jednostka' });
		expect(dialog).toBeInTheDocument();
		expect(
			screen.getAllByRole('combobox', { name: 'Jednostka', hidden: true })[0]
		).toHaveTextContent('g');
		await user.type(screen.getByLabelText('Nazwa'), 'kostka');
		expect(screen.getByLabelText('Wiele (5)')).toHaveValue('kostek');
		expect(screen.getByTestId('unit-example')).toHaveTextContent(
			'1 kostka · 2 kostki · 5 kostek · ½ kostki'
		);
		expect(screen.getByRole('switch', { name: /Tylko dla produktu produkt/ })).toBeChecked();
	});
});
