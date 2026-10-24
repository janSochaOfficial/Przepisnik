import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
	baseRecipe,
	matrix,
	optionKey,
	slotKey,
	variantA,
	variantB
} from '../../../tests/integration/mocks/variants';
import { picksOfVariant, SAVE_DELAY_MS, SelectionState } from './selection.svelte';

function make(over: Partial<ConstructorParameters<typeof SelectionState>[0]> = {}) {
	return new SelectionState({
		recipe: baseRecipe(),
		matrix: matrix(),
		selection: null,
		preferences: [],
		liquidUnits: 'kitchen',
		...over
	});
}

describe('SelectionState', () => {
	let requests: { url: string; body: unknown }[];

	beforeEach(() => {
		vi.useFakeTimers();
		requests = [];
		vi.stubGlobal(
			'fetch',
			vi.fn(async (req: Request) => {
				requests.push({ url: req.url, body: await req.json().catch(() => null) });
				return new Response('{}', { status: 200, headers: { 'content-type': 'application/json' } });
			})
		);
	});
	afterEach(() => {
		vi.useRealTimers();
		vi.unstubAllGlobals();
	});

	it('starts on the base when nothing is picked', () => {
		const s = make();
		expect(s.match.bestVariantId).toBe('base');
		expect(s.present.map((r) => r.ingredient?.product.name)).toEqual([
			'kurczak',
			'mleko',
			'cebula',
			'kardamon'
		]);
	});

	it('a pick switches the unpinned slots to the best compatible variant', () => {
		const s = make();
		s.pick(slotKey.chicken, optionKey.tofu);
		expect(s.match.bestVariantId).toBe(variantA);
		expect(s.cookIngredients.at(-1)?.baseAmount).toBe(10);
	});

	it('picks from two variants make a custom mix', () => {
		const s = make();
		s.pick(slotKey.chicken, optionKey.tofu);
		s.pick(slotKey.milk, optionKey.cream);
		expect(s.match.isCustomMix).toBe(true);
	});

	it('pins every slot to the variant of ?wariant=', () => {
		expect(Object.keys(picksOfVariant(matrix(), variantB))).toHaveLength(5);
		const s = make({ pinVariantId: variantB });
		expect(s.match.bestVariantId).toBe(variantB);
		expect(picksOfVariant(matrix(), 'nope')).toEqual({});
	});

	it('does not use ?wariant= when a selection is saved', () => {
		const s = make({
			pinVariantId: variantB,
			selection: { recipeId: 'r', picks: {}, updatedAt: '2026-10-10T10:00:00Z' }
		});
		expect(s.match.bestVariantId).toBe('base');
	});

	it('scales every amount from the anchor and resets', () => {
		const s = make();
		s.setScaling(slotKey.chicken, 600);
		expect(s.factor).toBe(1.5);
		const amounts = s.cookIngredients.map((i) => i.scaledBaseAmount ?? 0);
		[600, 375, 3.9, 7.5].forEach((want, n) => expect(amounts[n]).toBeCloseTo(want, 6));
		s.resetScaling();
		expect(s.factor).toBe(1);
		expect(s.cookIngredients[0]?.scaledBaseAmount).toBe(400);
	});

	it('saves once, one second after the last change', async () => {
		const s = make();
		s.pick(slotKey.chicken, optionKey.tofu);
		await vi.advanceTimersByTimeAsync(SAVE_DELAY_MS - 100);
		s.pick(slotKey.chicken, optionKey.tofu);
		s.setScaling(slotKey.chicken, 600);
		await vi.advanceTimersByTimeAsync(SAVE_DELAY_MS - 100);
		expect(requests).toHaveLength(0);
		await vi.advanceTimersByTimeAsync(200);
		expect(requests).toHaveLength(1);
		expect(requests[0]?.url).toMatch(/\/my-selection$/);
		expect(requests[0]?.body).toMatchObject({
			picks: { [slotKey.chicken]: { optionKey: optionKey.tofu, source: 'user' } },
			scaling: { anchorSlotKey: slotKey.chicken, anchorBaseAmount: 600 }
		});
	});

	it('flush sends a pending save at once', async () => {
		const s = make();
		s.pick(slotKey.chicken, optionKey.tofu);
		s.flush();
		await vi.advanceTimersByTimeAsync(0);
		expect(requests).toHaveLength(1);
	});

	it('a favourite variant wins once there are no pins and no memory', () => {
		const s = make({ pinVariantId: variantA });
		s.setFavorite(variantA, true);
		s.clearPins();
		expect(s.match.bestVariantId).toBe(variantA);
	});
});
