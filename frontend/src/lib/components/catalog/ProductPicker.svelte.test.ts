import '$lib/components/ui/testing';
import { render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { ProductRef } from '$lib/domain/types';
import ProductPicker from './ProductPicker.svelte';

const product = (id: string, name: string, isVerified: boolean): ProductRef => ({
	id,
	name,
	category: 'vegetables',
	measureKind: 'mass',
	displayClass: 'weight',
	hasSizes: false,
	allowHalf: false,
	gramsPerMl: null,
	gramsPerPiece: null,
	defaultPackageAmount: null,
	pieceForms: null,
	isVerified
});

const CATALOG = [product('p1', 'cebula', true), product('p2', 'cebulka marynowana', false)];

const get = vi.hoisted(() => vi.fn());
vi.mock('$lib/api/client', async (importOriginal) => ({
	...(await importOriginal<typeof import('$lib/api/client')>()),
	createApi: () => ({ GET: get })
}));

function option(text: string): Promise<HTMLElement> {
	return waitFor(() => {
		const match = screen
			.getAllByRole('option', { hidden: true })
			.find((o) => o.textContent?.replace(/\s+/g, ' ').trim().startsWith(text));
		if (!match) throw new Error(`no option "${text}"`);
		return match;
	});
}

describe('ProductPicker', () => {
	beforeEach(() => {
		get.mockImplementation(async (_path: string, init: { params: { query: { q: string } } }) => {
			const q = init.params.query.q.toLocaleLowerCase('pl');
			const data = CATALOG.filter((p) => p.name.includes(q));
			return { data, response: new Response(null, { status: 200 }) };
		});
	});

	afterEach(() => {
		get.mockReset();
	});

	it('marks user-added products next to the category', async () => {
		const user = userEvent.setup();
		render(ProductPicker);

		await user.type(screen.getByRole('combobox'), 'ceb');

		expect((await option('cebula')).textContent).toContain('warzywa');
		const added = await option('cebulka marynowana');
		expect(added.textContent).toContain('warzywa · dodany przez użytkownika');
		expect((await option('cebula')).textContent).not.toContain('dodany przez użytkownika');
	});

	it('selects the product whose name matches the typed text, without asking', async () => {
		const user = userEvent.setup();
		const onValueChange = vi.fn();
		render(ProductPicker, { confirmNew: true, onValueChange });

		await user.type(screen.getByRole('combobox'), 'Cebula');
		await user.tab();

		await waitFor(() => expect(onValueChange).toHaveBeenCalledWith(CATALOG[0]));
		expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
	});

	it('asks before adding an unknown name, and "Nie dodawaj" clears the field', async () => {
		const user = userEvent.setup();
		render(ProductPicker, { confirmNew: true });
		const input = screen.getByRole('combobox');

		await user.type(input, 'kozieradka');
		await user.tab();

		const prompt = await screen.findByRole('alertdialog');
		expect(prompt).toHaveTextContent('„kozieradka” nie ma w bazie produktów. Dodać?');
		await user.click(screen.getByRole('button', { name: 'Nie dodawaj' }));

		await waitFor(() => expect(input).toHaveValue(''));
	});

	it('"Dodaj do bazy" opens the new-product form with the name', async () => {
		const user = userEvent.setup();
		render(ProductPicker, { confirmNew: true });

		await user.type(screen.getByRole('combobox'), 'kozieradka');
		await user.tab();
		await user.click(await screen.findByRole('button', { name: 'Dodaj do bazy' }));

		expect(await screen.findByLabelText('Nazwa produktu')).toHaveValue('kozieradka');
	});

	it('does not ask without confirmNew', async () => {
		const user = userEvent.setup();
		render(ProductPicker);

		await user.type(screen.getByRole('combobox'), 'kozieradka');
		await user.tab();

		await new Promise((resolve) => setTimeout(resolve, 50));
		expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
	});
});
