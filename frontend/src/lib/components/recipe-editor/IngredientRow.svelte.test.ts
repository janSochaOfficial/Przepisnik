import '$lib/components/ui/testing';
import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import type { ProductRef } from '$lib/domain/types';
import IngredientRowHarness from './IngredientRowHarness.test.svelte';

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
		expect(await unitOptions()).toEqual(['g', 'kg']);
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
		expect(await unitOptions()).toEqual(['szt.']);
		expect(screen.getByRole('combobox', { name: 'Wielkość' })).toBeInTheDocument();
	});

	it('hides the amount for "do smaku"', async () => {
		const user = userEvent.setup();
		render(IngredientRowHarness, { product: base, unit: 'g' });
		await user.click(screen.getByRole('switch', { name: /Do smaku/ }));
		expect(screen.queryByLabelText('Ilość')).not.toBeInTheDocument();
	});
});
