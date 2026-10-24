import { describe, expect, it } from 'vitest';
import type { Slot } from '$lib/domain/types';
import {
	baseIngredientIds,
	baseRecipe,
	matrix,
	optionKey,
	resolved,
	slotKey,
	variantA,
	variantC
} from '../../../../tests/integration/mocks/variants';
import { buildRows, orderSlots } from './rows';
import { draftFromSelection, variantDraft } from './variant-draft';

const slot = (key: string, afterSlotKey?: string | null): Slot => ({
	key,
	label: key,
	options: [],
	...(afterSlotKey === undefined ? {} : { afterSlotKey })
});

describe('orderSlots', () => {
	it('puts added slots after the base slot they follow', () => {
		const ordered = orderSlots([
			slot('i:1'),
			slot('i:2'),
			slot('p:x', 'i:1'),
			slot('p:y', 'i:1'),
			slot('p:z', 'i:2')
		]);
		expect(ordered.map((s) => s.key)).toEqual(['i:1', 'p:x', 'p:y', 'i:2', 'p:z']);
	});

	it('puts a null afterSlotKey first and a missing or unknown one last', () => {
		const ordered = orderSlots([
			slot('i:1'),
			slot('p:old'),
			slot('p:first', null),
			slot('p:ghost', 'i:gone')
		]);
		expect(ordered.map((s) => s.key)).toEqual(['p:first', 'i:1', 'p:old', 'p:ghost']);
	});
});

describe('buildRows', () => {
	it('marks changed and absent rows from the picks', () => {
		const m = matrix();
		const rows = buildRows(m, {
			[slotKey.chicken]: { optionKey: optionKey.tofu, source: 'user' },
			[slotKey.pepper]: { optionKey: 'none', source: 'auto' }
		});
		expect(rows.map((r) => r.slotKey)).toEqual([
			slotKey.chicken,
			slotKey.pepper,
			slotKey.milk,
			expect.any(String),
			slotKey.cardamom
		]);
		expect(rows[0]).toMatchObject({ changed: true, isBase: true });
		expect(rows[0]?.ingredient?.product.name).toBe('tofu');
		expect(rows[1]).toMatchObject({ ingredient: null, isBase: false });
	});
});

describe('variantDraft', () => {
	it('keeps base rows with their ids and values of the variant', () => {
		const draft = variantDraft(baseRecipe(), resolved[variantA]!);
		expect(draft.ingredients.map((i) => [i.id, i.product?.name, i.base, i.removed])).toEqual([
			[baseIngredientIds.chicken, 'tofu', true, false],
			[baseIngredientIds.milk, 'mleko', true, false],
			[baseIngredientIds.onion, 'cebula', true, false],
			[baseIngredientIds.cardamom, 'kardamon', true, false]
		]);
		expect(draft.ingredients[3]?.value).toBe(2);
	});

	it('gives rows the base lacks no id, and keeps their place', () => {
		const draft = variantDraft(baseRecipe(), resolved[variantC]!);
		expect(draft.ingredients.map((i) => [i.product?.name, i.id === undefined, i.base])).toEqual([
			['kurczak', false, true],
			['papryka', true, false],
			['mleko', false, true],
			['cebula', false, true],
			['kardamon', false, true]
		]);
	});

	it('brings back base rows the variant removed, marked removed', () => {
		const withoutMilk = {
			...resolved[variantA]!,
			ingredients: resolved[variantA]!.ingredients.filter((i) => i.product.name !== 'mleko')
		};
		const draft = variantDraft(baseRecipe(), withoutMilk);
		const milk = draft.ingredients.find((i) => i.id === baseIngredientIds.milk);
		expect(milk).toMatchObject({ removed: true, base: true });
		expect(draft.ingredients.map((i) => i.product?.name)).toEqual([
			'tofu',
			'mleko',
			'cebula',
			'kardamon'
		]);
	});

	it('marks base steps and renders a replaced step with the base id', () => {
		const draft = variantDraft(baseRecipe(), resolved[variantA]!);
		expect(draft.instructionSteps.every((s) => s.base)).toBe(true);
	});
});

describe('draftFromSelection', () => {
	it('uses the base id for base slots and none for added slots; "none" on a base slot is removed', () => {
		const m = matrix();
		const rows = buildRows(m, {
			[slotKey.chicken]: { optionKey: optionKey.tofu, source: 'user' },
			[slotKey.milk]: { optionKey: 'none', source: 'user' },
			[slotKey.pepper]: { optionKey: optionKey.pepper, source: 'user' },
			...Object.fromEntries(
				m.slots
					.filter((sl) => sl.key.includes('f1003') || sl.key === slotKey.cardamom)
					.map((sl) => [sl.key, { optionKey: sl.options[0]!.key, source: 'auto' as const }])
			)
		});
		const draft = draftFromSelection(baseRecipe(), resolved[variantA]!, rows);
		expect(
			draft.ingredients.map((i) => [i.product?.name, i.id ?? null, i.base, i.removed])
		).toEqual([
			['tofu', baseIngredientIds.chicken, true, false],
			['papryka', null, false, false],
			['mleko', baseIngredientIds.milk, true, true],
			['cebula', baseIngredientIds.onion, true, false],
			['kardamon', baseIngredientIds.cardamom, true, false]
		]);
	});
});
