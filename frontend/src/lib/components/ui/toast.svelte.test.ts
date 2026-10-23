import './testing';
import { render, screen } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { TOAST_ACTION_DURATION, TOAST_DURATION, ToastQueue } from './toast.svelte';
import Toaster from './Toaster.svelte';

describe('ToastQueue', () => {
	beforeEach(() => {
		vi.useFakeTimers();
	});

	afterEach(() => {
		vi.useRealTimers();
	});

	it('hides a toast by itself after 5 s', () => {
		const queue = new ToastQueue();
		queue.show('Zapisano przepis.');

		vi.advanceTimersByTime(TOAST_DURATION - 1);
		expect(queue.items).toHaveLength(1);
		vi.advanceTimersByTime(1);
		expect(queue.items).toHaveLength(0);
	});

	it('keeps a toast with an action for 8 s', () => {
		const queue = new ToastQueue();
		queue.show('Usunięto produkt.', { action: { label: 'Cofnij', onAction: () => {} } });

		vi.advanceTimersByTime(TOAST_DURATION);
		expect(queue.items).toHaveLength(1);
		vi.advanceTimersByTime(TOAST_ACTION_DURATION - TOAST_DURATION);
		expect(queue.items).toHaveLength(0);
	});

	it('keeps a toast with duration 0 until dismissed', () => {
		const queue = new ToastQueue();
		const id = queue.show('Brak połączenia z serwerem.', { duration: 0 });

		vi.advanceTimersByTime(60_000);
		expect(queue.items).toHaveLength(1);
		queue.dismiss(id);
		expect(queue.items).toHaveLength(0);
	});

	it('does not count down while paused', () => {
		const queue = new ToastQueue();
		const id = queue.show('Zapisano przepis.');

		vi.advanceTimersByTime(2000);
		queue.pause(id);
		vi.advanceTimersByTime(10_000);
		expect(queue.items).toHaveLength(1);
		queue.resume(id);
		vi.advanceTimersByTime(TOAST_DURATION - 2000);
		expect(queue.items).toHaveLength(0);
	});

	it('shows at most three toasts, dropping the oldest', () => {
		const queue = new ToastQueue();
		['a', 'b', 'c', 'd'].forEach((m) => queue.show(m));

		expect(queue.items.map((i) => i.message)).toEqual(['b', 'c', 'd']);
	});
});

describe('Toaster', () => {
	it('runs the action once and hides the toast', async () => {
		const queue = new ToastQueue();
		const onAction = vi.fn();
		render(Toaster, { queue });
		queue.show('Usunięto produkt.', { action: { label: 'Cofnij', onAction } });
		const user = userEvent.setup();

		await user.click(await screen.findByRole('button', { name: 'Cofnij' }));

		expect(onAction).toHaveBeenCalledTimes(1);
		expect(screen.queryByText('Usunięto produkt.')).not.toBeInTheDocument();
	});

	it('announces toasts politely and errors assertively', async () => {
		const queue = new ToastQueue();
		render(Toaster, { queue });

		queue.success('Zapisano przepis.');
		queue.error('Brak połączenia z serwerem.');

		expect(await screen.findByRole('status')).toHaveTextContent('Zapisano przepis.');
		expect(screen.getByRole('alert')).toHaveTextContent('Brak połączenia z serwerem.');
	});

	it('disappears after the timeout', async () => {
		vi.useFakeTimers();
		try {
			const queue = new ToastQueue();
			render(Toaster, { queue });
			queue.show('Zapisano przepis.');
			expect(await screen.findByText('Zapisano przepis.')).toBeInTheDocument();

			await vi.advanceTimersByTimeAsync(TOAST_DURATION);

			expect(screen.queryByText('Zapisano przepis.')).not.toBeInTheDocument();
		} finally {
			vi.useRealTimers();
		}
	});

	it('closes with the dismiss button', async () => {
		const queue = new ToastQueue();
		render(Toaster, { queue });
		queue.show('Zapisano przepis.');
		const user = userEvent.setup();

		await user.click(await screen.findByRole('button', { name: 'Zamknij powiadomienie' }));

		expect(queue.items).toHaveLength(0);
	});
});
