import './testing';
import { render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import Combobox from './Combobox.svelte';
import type { Option } from './types';

const PRODUCTS: Option[] = [
	{ value: 'p1', label: 'mleko' },
	{ value: 'p2', label: 'mleko kokosowe' },
	{ value: 'p3', label: 'masło' }
];

function fakeSearch() {
	return vi.fn((query: string, _signal: AbortSignal) =>
		Promise.resolve(PRODUCTS.filter((p) => p.label.includes(query)))
	);
}

// floating-ui keeps the list `visibility: hidden` until it can measure, which jsdom never does,
// so options are found with `hidden: true` and by their text (hidden nodes have no accessible name).
function findOption(text: string): Promise<HTMLElement> {
	return waitFor(() => {
		const match = screen
			.getAllByRole('option', { hidden: true })
			.find((o) => o.textContent?.replace(/\s+/g, ' ').trim() === text);
		if (!match) throw new Error(`no option "${text}"`);
		return match;
	});
}

function optionTexts(): string[] {
	return screen
		.queryAllByRole('option', { hidden: true })
		.map((o) => o.textContent?.replace(/\s+/g, ' ').trim() ?? '');
}

describe('Combobox', () => {
	beforeEach(() => {
		vi.useFakeTimers({ shouldAdvanceTime: true });
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	function setup(extra: Record<string, unknown> = {}) {
		const search = fakeSearch();
		const onValueChange = vi.fn();
		const onCreate = vi.fn();
		render(Combobox, { label: 'Składnik', search, onValueChange, onCreate, ...extra });
		const user = userEvent.setup({ advanceTimers: vi.advanceTimersByTime });
		const input = screen.getByRole('combobox', { name: 'Składnik' });
		return { search, onValueChange, onCreate, user, input };
	}

	it('searches once, after the user stops typing', async () => {
		const { search, user, input } = setup();

		await user.type(input, 'mle');
		expect(search).not.toHaveBeenCalled();

		await vi.advanceTimersByTimeAsync(250);

		expect(search).toHaveBeenCalledTimes(1);
		expect(search).toHaveBeenCalledWith('mle', expect.any(AbortSignal));
		expect(await findOption('mleko kokosowe')).toBeInTheDocument();
	});

	it('aborts the previous request when the user types again', async () => {
		const { search, user, input } = setup();

		await user.type(input, 'm');
		await vi.advanceTimersByTimeAsync(250);
		await user.type(input, 'a');
		await vi.advanceTimersByTimeAsync(250);

		expect(search).toHaveBeenCalledTimes(2);
		const firstSignal = search.mock.calls[0]?.[1];
		expect(firstSignal?.aborted).toBe(true);
		expect(search).toHaveBeenLastCalledWith('ma', expect.any(AbortSignal));
	});

	it('picks an option', async () => {
		const { onValueChange, user, input } = setup();

		await user.type(input, 'mas');
		await vi.advanceTimersByTimeAsync(250);
		await user.click(await findOption('masło'));

		expect(onValueChange).toHaveBeenLastCalledWith({ value: 'p3', label: 'masło' });
	});

	it('offers to create what was typed and calls onCreate with it', async () => {
		const { onCreate, user, input } = setup();

		await user.type(input, 'mleko owsiane');
		await vi.advanceTimersByTimeAsync(250);
		const create = await findOption('Dodaj „mleko owsiane”');
		await user.click(create);

		expect(onCreate).toHaveBeenCalledWith('mleko owsiane');
	});

	it('hides the create item when an option has exactly the typed label', async () => {
		const { user, input } = setup();

		await user.type(input, 'masło');
		await vi.advanceTimersByTimeAsync(250);
		await findOption('masło');

		expect(optionTexts().some((text) => text.startsWith('Dodaj'))).toBe(false);
	});

	it('shows no create item without onCreate', async () => {
		const { user, input } = setup({ onCreate: undefined });

		await user.type(input, 'xyz');
		await vi.advanceTimersByTimeAsync(250);

		expect(await screen.findByText('Nic nie znaleziono.')).toBeInTheDocument();
		expect(screen.queryByRole('option', { hidden: true })).not.toBeInTheDocument();
	});

	it('calls onCommit with the typed text when the field loses focus unpicked', async () => {
		const onCommit = vi.fn();
		const { user, input } = setup({ onCommit });

		await user.type(input, 'kozieradka');
		await user.tab();
		await vi.advanceTimersByTimeAsync(10);

		expect(onCommit).toHaveBeenCalledTimes(1);
		expect(onCommit).toHaveBeenCalledWith('kozieradka');
	});

	it('does not call onCommit for an empty field, or after an option was picked', async () => {
		const onCommit = vi.fn();
		const { user, input } = setup({ onCommit });

		await user.click(input);
		await user.tab();
		await vi.advanceTimersByTimeAsync(10);
		expect(onCommit).not.toHaveBeenCalled();

		await user.type(input, 'mas');
		await vi.advanceTimersByTimeAsync(250);
		await user.click(await findOption('masło'));
		await user.tab();
		await vi.advanceTimersByTimeAsync(10);
		expect(onCommit).not.toHaveBeenCalled();
	});
});
