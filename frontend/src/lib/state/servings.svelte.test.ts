import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import {
	baseRecipe,
	matrix,
	resolved,
	slotKey,
	variantB
} from '../../../tests/integration/mocks/variants';
import { SAVE_DELAY_MS, SelectionState } from './selection.svelte';

// The servings parts of the recipe view state (PROJECT §5.7, D-37).
function make(servings: number | null = 4) {
	return new SelectionState({
		recipe: baseRecipe({ servings }),
		matrix: matrix(),
		selection: null,
		preferences: [],
		liquidUnits: 'kitchen'
	});
}

describe('SelectionState servings', () => {
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

	it('shows the recipe servings unscaled', () => {
		const s = make();
		expect(s.servings).toBe(4);
		expect(s.shownServings).toEqual({ value: 4, approximate: false });
		expect(s.factor).toBe(1);
	});

	it('choosing 6 of 4 servings scales every amount ×1.5 and is saved as the servings form', async () => {
		const s = make();
		s.setServings(6);
		expect(s.factor).toBe(1.5);
		expect(s.cookIngredients[0]?.scaledBaseAmount).toBe(600);
		expect(s.shownServings).toEqual({ value: 6, approximate: false });
		await vi.advanceTimersByTimeAsync(SAVE_DELAY_MS + 100);
		expect(requests[0]?.body).toMatchObject({ scaling: { kind: 'servings', servings: 6 } });
	});

	it('clamps the choice to 1–100', () => {
		const s = make();
		s.setServings(0);
		expect(s.scaling).toEqual({ kind: 'servings', servings: 1 });
		s.setServings(500);
		expect(s.scaling).toEqual({ kind: 'servings', servings: 100 });
	});

	it('an ingredient anchor replaces a servings choice, and the line shows servings × factor', () => {
		const s = make();
		s.setServings(6);
		s.setScaling(slotKey.chicken, 300); // 400 g → 300 g: ×0.75
		expect(s.scaling).toEqual({ anchorSlotKey: slotKey.chicken, anchorBaseAmount: 300 });
		expect(s.factor).toBe(0.75);
		expect(s.shownServings).toEqual({ value: 3, approximate: false });
		s.setScaling(slotKey.chicken, 230); // ×0.575 → 2.3 → ~2 ½
		expect(s.shownServings).toEqual({ value: 2.5, approximate: true });
		s.setServings(2);
		expect(s.scaling).toEqual({ kind: 'servings', servings: 2 });
		expect(s.factor).toBe(0.5);
	});

	it('Przywróć resets a servings choice', () => {
		const s = make();
		s.setServings(8);
		s.resetScaling();
		expect(s.scaling).toBeNull();
		expect(s.factor).toBe(1);
	});

	it('a recipe without servings shows no servings line and ignores a servings choice', () => {
		const s = make(null);
		expect(s.shownServings).toBeNull();
		s.scaling = { kind: 'servings', servings: 6 };
		expect(s.factor).toBe(1);
		s.setScaling(slotKey.chicken, 600);
		expect(s.factor).toBe(1.5);
		expect(s.shownServings).toBeNull();
	});

	it("opening a variant keeps the servings choice, recomputed against the variant's servings", () => {
		const s = make();
		s.resolved = {
			[variantB]: { ...(resolved[variantB] as (typeof resolved)[string]), servings: 8 }
		};
		s.setServings(4);
		expect(s.factor).toBe(1);
		expect(s.openVariant(variantB)).toBe(true);
		expect(s.scaling).toEqual({ kind: 'servings', servings: 4 });
		expect(s.servings).toBe(8);
		expect(s.factor).toBe(0.5);
		expect(s.cookIngredients[0]?.scaledBaseAmount).toBe(200);
	});

	it('restores a remembered servings choice', () => {
		const s = new SelectionState({
			recipe: baseRecipe({ servings: 2 }),
			matrix: matrix(),
			selection: {
				recipeId: 'r',
				picks: {},
				scaling: { kind: 'servings', servings: 5 },
				updatedAt: '2026-10-10T10:00:00Z'
			},
			preferences: [],
			liquidUnits: 'kitchen'
		});
		expect(s.factor).toBe(2.5);
	});
});
