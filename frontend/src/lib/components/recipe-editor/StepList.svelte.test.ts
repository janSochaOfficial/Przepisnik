import '$lib/components/ui/testing';
import { render, screen, waitFor } from '@testing-library/svelte';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { imageBlock, textBlock } from './blocks';
import type { DraftStep } from './draft';
import StepListHarness from './StepListHarness.test.svelte';

vi.mock('./upload', async (importOriginal) => ({
	...(await importOriginal<typeof import('./upload')>()),
	uploadImage: vi.fn(async () => ({
		id: 'img-1',
		url: '/api/v1/images/img-1',
		thumbnailUrl: '/api/v1/images/img-1?size=thumb',
		width: 10,
		height: 10
	}))
}));

let counter = 0;
const step = (blocks: DraftStep['blocks'], base = false): DraftStep => ({
	key: `s-${++counter}`,
	blocks,
	base,
	removed: false
});

const out = () => JSON.parse(screen.getByTestId('out').textContent ?? '[]') as unknown[];

describe('StepList block editor', () => {
	it('splits the text at the cursor and inserts the uploaded photo', async () => {
		const user = userEvent.setup();
		const { container } = render(StepListHarness, {
			initial: [step([textBlock('Pierwszy akapit.\nDrugi akapit.')])]
		});

		const textarea = screen.getByLabelText('Tekst kroku') as HTMLTextAreaElement;
		textarea.focus();
		textarea.setSelectionRange(16, 16);
		// Pressing the button moves focus away, which is when the cursor is remembered.
		await user.click(screen.getByRole('button', { name: 'Wstaw zdjęcie' }));
		const input = container.querySelector('input[type="file"]') as HTMLInputElement;
		await user.upload(input, new File(['x'], 'a.png', { type: 'image/png' }));

		await waitFor(() =>
			expect(out()).toEqual([
				{
					removed: false,
					blocks: [
						{ type: 'text', text: 'Pierwszy akapit.' },
						{ type: 'image', imageId: 'img-1' },
						{ type: 'text', text: 'Drugi akapit.' }
					]
				}
			])
		);
		expect(screen.getAllByLabelText('Tekst kroku')).toHaveLength(2);
	});

	it('removing the photo joins the text again', async () => {
		const user = userEvent.setup();
		render(StepListHarness, {
			initial: [step([textBlock('przed'), imageBlock('img-9', 'podpis'), textBlock('po')])]
		});

		await user.click(screen.getByRole('button', { name: 'Usuń zdjęcie' }));

		expect(out()).toEqual([{ removed: false, blocks: [{ type: 'text', text: 'przed\npo' }] }]);
	});

	it('keeps the caption of a photo', async () => {
		const user = userEvent.setup();
		render(StepListHarness, {
			initial: [step([textBlock('a'), imageBlock('img-9'), textBlock('b')])]
		});

		await user.type(screen.getByLabelText(/Podpis zdjęcia/), 'Gotowe');

		expect(out()).toEqual([
			{
				removed: false,
				blocks: [
					{ type: 'text', text: 'a' },
					{ type: 'image', imageId: 'img-9', caption: 'Gotowe' },
					{ type: 'text', text: 'b' }
				]
			}
		]);
	});

	it('in a variant, a base step is marked removed instead of deleted, and can be restored', async () => {
		const user = userEvent.setup();
		render(StepListHarness, {
			mode: 'variant',
			initial: [step([textBlock('bazowy')], true), step([textBlock('nowy')])]
		});

		await user.click(screen.getByRole('button', { name: 'Usuń krok w wariancie' }));
		expect(out()).toMatchObject([{ removed: true }, { removed: false }]);

		await user.click(screen.getByRole('button', { name: 'Przywróć krok' }));
		expect(out()).toMatchObject([{ removed: false }, { removed: false }]);
	});

	it('does not let a base step move in a variant', () => {
		render(StepListHarness, {
			mode: 'variant',
			initial: [step([textBlock('a')], true), step([textBlock('b')], true)]
		});
		for (const button of screen.getAllByRole('button', { name: /Przesuń krok/ })) {
			expect(button).toBeDisabled();
		}
	});
});
