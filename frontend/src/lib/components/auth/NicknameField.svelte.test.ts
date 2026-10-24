import '$lib/components/ui/testing';
import { render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, describe, expect, it, vi } from 'vitest';
import NicknameField from './NicknameField.svelte';

const taken = ['bartek'];
const fetchMock = vi.fn(async (input: Request) => {
	const nickname = new URL(input.url).searchParams.get('nickname') ?? '';
	return new Response(JSON.stringify({ available: !taken.includes(nickname.toLowerCase()) }), {
		headers: { 'content-type': 'application/json' }
	});
});

function setup(props: Record<string, unknown> = {}) {
	vi.stubGlobal('fetch', fetchMock);
	return render(NicknameField, { debounceMs: 0, ...props });
}

afterEach(() => {
	fetchMock.mockClear();
	vi.unstubAllGlobals();
});

const field = () => screen.getByLabelText('Pseudonim');

describe('NicknameField', () => {
	it('explains the rules and does not complain about an empty field', () => {
		setup();
		expect(screen.getByText('Od 3 do 30 znaków: litery, cyfry, _ lub -.')).toBeInTheDocument();
		expect(field()).not.toHaveAttribute('aria-invalid');
		expect(fetchMock).not.toHaveBeenCalled();
	});

	it('rejects characters outside the rules without asking the server', async () => {
		const user = userEvent.setup();
		setup();

		await user.click(field());
		await user.paste('ania kowalska');

		expect(screen.getByText('Użyj tylko liter, cyfr, _ lub -, bez spacji.')).toBeInTheDocument();
		expect(field()).toHaveAttribute('aria-invalid', 'true');
		expect(fetchMock).not.toHaveBeenCalled();
	});

	it('reports a too-short name only after the field loses focus', async () => {
		const user = userEvent.setup();
		setup();

		await user.type(field(), 'an');
		expect(screen.queryByText('Pseudonim musi mieć co najmniej 3 znaki.')).not.toBeInTheDocument();
		await user.tab();

		expect(screen.getByText('Pseudonim musi mieć co najmniej 3 znaki.')).toBeInTheDocument();
	});

	it('reports a name that is too long', async () => {
		const user = userEvent.setup();
		setup();

		await user.type(field(), 'a'.repeat(31));

		expect(screen.getByText('Pseudonim może mieć najwyżej 30 znaków.')).toBeInTheDocument();
	});

	it('asks the server and says when the name is free', async () => {
		const user = userEvent.setup();
		setup();

		await user.type(field(), 'Zażółć');

		await waitFor(() =>
			expect(screen.getAllByText('Pseudonim jest wolny.').length).toBeGreaterThan(0)
		);
		expect(fetchMock).toHaveBeenCalled();
		const last = fetchMock.mock.calls.at(-1)?.[0];
		expect(new URL(last!.url).searchParams.get('nickname')).toBe('Zażółć');
	});

	it('shows an inline error for a taken name, whatever its case', async () => {
		const user = userEvent.setup();
		setup();

		await user.type(field(), 'Bartek');

		expect(await screen.findByText('Ten pseudonim jest już zajęty.')).toBeInTheDocument();
		expect(field()).toHaveAttribute('aria-invalid', 'true');
	});

	it('never reports your own nickname as taken, and skips the lookup', async () => {
		setup({ value: 'bartek', current: 'Bartek' });

		await waitFor(() =>
			expect(screen.getAllByText('Pseudonim jest wolny.').length).toBeGreaterThan(0)
		);
		expect(fetchMock).not.toHaveBeenCalled();
		expect(screen.queryByText('Ten pseudonim jest już zajęty.')).not.toBeInTheDocument();
	});

	it('shows a refusal from the server and clears it when the user edits', async () => {
		const user = userEvent.setup();
		const onedit = vi.fn();
		setup({ value: 'zosia', error: 'Ten pseudonim jest już zajęty.', onedit });

		expect(screen.getByText('Ten pseudonim jest już zajęty.')).toBeInTheDocument();
		await user.type(field(), 'x');

		expect(onedit).toHaveBeenCalled();
	});
});
