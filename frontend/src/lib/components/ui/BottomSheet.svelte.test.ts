import { setViewport } from './testing';
import { render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import Harness from './BottomSheet.test.svelte';

describe.each([
	['mobile', 'sheet'],
	['desktop', 'popover']
] as const)('BottomSheet on %s', (viewport, shape) => {
	async function open() {
		setViewport(viewport);
		const onOpenChange = vi.fn();
		render(Harness, { onOpenChange });
		const user = userEvent.setup();
		const trigger = screen.getByRole('button', { name: 'open' });
		await user.click(trigger);
		const dialog = await screen.findByRole('dialog', { hidden: true });
		return { user, trigger, dialog, onOpenChange };
	}

	it(`opens as a ${shape} named by its title`, async () => {
		const { dialog } = await open();

		expect(dialog).toHaveAttribute('data-shape', shape);
		expect(dialog).toHaveAccessibleName('Zamiennik');
	});

	it('traps focus inside while open', async () => {
		const { user, dialog } = await open();
		await waitFor(() => expect(dialog).toContainElement(document.activeElement as HTMLElement));

		for (let i = 0; i < 6; i++) {
			await user.tab();
			expect(dialog).toContainElement(document.activeElement as HTMLElement);
		}
		await user.tab({ shift: true });
		expect(dialog).toContainElement(document.activeElement as HTMLElement);
	});

	it('closes on Escape and returns focus to the trigger', async () => {
		const { user, trigger, onOpenChange } = await open();

		await user.keyboard('{Escape}');

		await waitFor(() => expect(screen.queryByRole('dialog', { hidden: true })).toBeNull());
		expect(onOpenChange).toHaveBeenLastCalledWith(false);
		await waitFor(() => expect(trigger).toHaveFocus());
	});
});
