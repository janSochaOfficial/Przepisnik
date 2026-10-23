import './testing';
import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import NumberField from './NumberField.svelte';

function setup(props: Partial<{ value: number | null; min: number; max: number }> = {}) {
	const onValueChange = vi.fn();
	const result = render(NumberField, { label: 'Ilość', onValueChange, ...props });
	const input = screen.getByRole('textbox', { name: 'Ilość' });
	return { ...result, input, onValueChange, user: userEvent.setup() };
}

describe('NumberField', () => {
	it('reads "1,5" as 1.5', async () => {
		const { input, onValueChange, user } = setup();

		await user.type(input, '1,5');

		expect(onValueChange).toHaveBeenLastCalledWith(1.5);
		expect(input).not.toHaveAttribute('aria-invalid');
	});

	it('rejects "abc" with a Polish message and no value', async () => {
		const { input, onValueChange, user } = setup({ value: 2 });

		await user.clear(input);
		await user.type(input, 'abc');

		expect(onValueChange).toHaveBeenLastCalledWith(null);
		expect(onValueChange).not.toHaveBeenCalledWith(expect.any(Number));
		expect(input).toHaveAttribute('aria-invalid', 'true');
		expect(input).toHaveAccessibleDescription('Wpisz liczbę, np. 1,5.');
	});

	it('shows a value from outside with a decimal comma', () => {
		const { input } = setup({ value: 0.25 });

		expect(input).toHaveValue('0,25');
		expect(input).toHaveAttribute('inputmode', 'decimal');
	});

	it('normalises a dot to a comma when the field loses focus', async () => {
		const { input, onValueChange, user } = setup();

		await user.type(input, '2.5');
		await user.tab();

		expect(onValueChange).toHaveBeenLastCalledWith(2.5);
		expect(input).toHaveValue('2,5');
	});

	it('reports a value below min and does not emit it', async () => {
		const { input, onValueChange, user } = setup({ min: 1 });

		await user.type(input, '0,5');

		expect(onValueChange).not.toHaveBeenCalledWith(0.5);
		expect(input).toHaveAccessibleDescription('Najmniejsza wartość to 1.');
	});

	it('updates when the value changes from outside', async () => {
		const { input, rerender } = setup({ value: 1 });

		await rerender({ value: 3.75 });

		expect(input).toHaveValue('3,75');
	});
});
