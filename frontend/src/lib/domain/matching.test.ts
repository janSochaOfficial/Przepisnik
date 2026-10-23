import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';
import { clearUserPicks, draftFromMatch, matchVariants, pin } from './matching';
import type { MatchResult } from './matching';
import type { Ingredient, Pick, ProductPreference, ResolvedRecipe, VariantMatrix } from './types';

const vectorsDir = resolve(process.cwd(), '../shared/test-vectors') + '/'; // vitest runs from frontend/

function loadCases<T>(kind: string): T[] {
	const dir = `${vectorsDir}${kind}/`;
	return readdirSync(dir)
		.filter((f) => f.endsWith('.json'))
		.sort()
		.flatMap((f) => (JSON.parse(readFileSync(`${dir}${f}`, 'utf8')) as { cases: T[] }).cases);
}

type VariantCase = { id: string; expectedMatrix: VariantMatrix };
type MatchCase = {
	id: string;
	matrix?: VariantMatrix;
	matrixFromCase?: string;
	picks: Record<string, Pick>;
	preferences: ProductPreference[];
	expected: {
		bestVariantId?: string;
		isCustomMix?: boolean;
		compatibleVariantIds?: string[];
		picks?: Record<string, Pick>;
		absentPickSlots?: string[];
		ingredientIds?: string[];
		optionOrder?: Record<string, string[]>;
		optionCompatible?: Record<string, boolean[]>;
		optionSupport?: Record<string, number[]>;
		selected?: Record<string, Pick>;
	};
};

const variantCases = loadCases<VariantCase>('variants');
const matchCases = loadCases<MatchCase>('matching');

function deepFreeze<T>(value: T): T {
	if (value && typeof value === 'object' && !Object.isFrozen(value)) {
		Object.freeze(value);
		for (const child of Object.values(value)) deepFreeze(child);
	}
	return value;
}

function matrixOf(c: MatchCase): VariantMatrix {
	if (c.matrix) return structuredClone(c.matrix);
	const source = variantCases.find((v) => v.id === c.matrixFromCase);
	if (!source) throw new Error(`unknown matrixFromCase ${c.matrixFromCase}`);
	return structuredClone(source.expectedMatrix);
}

describe('test vectors (matching/*.json)', () => {
	it('has at least 25 cases', () => {
		expect(matchCases.length).toBeGreaterThanOrEqual(25);
	});

	describe.each(matchCases)('$id', (c) => {
		it('matches the expected essentials', () => {
			const matrix = deepFreeze(matrixOf(c));
			const picks = deepFreeze(structuredClone(c.picks));
			const preferences = deepFreeze(structuredClone(c.preferences));
			const result = matchVariants({ matrix, picks, preferences });
			const e = c.expected;

			if (e.bestVariantId !== undefined) expect(result.bestVariantId).toBe(e.bestVariantId);
			if (e.isCustomMix !== undefined) expect(result.isCustomMix).toBe(e.isCustomMix);
			if (e.compatibleVariantIds)
				expect(result.compatibleVariantIds).toEqual(e.compatibleVariantIds);
			for (const [slot, pick] of Object.entries(e.picks ?? {}))
				expect(result.picks[slot], `pick ${slot}`).toEqual(pick);
			for (const slot of e.absentPickSlots ?? []) expect(result.picks).not.toHaveProperty(slot);
			if (e.ingredientIds)
				expect(result.ingredients.map((i) => i.id)).toEqual(e.ingredientIds);
			for (const [slot, keys] of Object.entries(e.optionOrder ?? {}))
				expect(result.options[slot].map((o) => o.option.key), `order ${slot}`).toEqual(keys);
			for (const [slot, flags] of Object.entries(e.optionCompatible ?? {}))
				expect(result.options[slot].map((o) => o.compatible)).toEqual(flags);
			for (const [slot, counts] of Object.entries(e.optionSupport ?? {}))
				expect(result.options[slot].map((o) => o.supportCount)).toEqual(counts);
			for (const [slot, pick] of Object.entries(e.selected ?? {})) {
				const selected = result.options[slot].filter((o) => o.selected);
				expect(selected).toHaveLength(1);
				expect(selected[0].option.key).toBe(pick.optionKey);
				expect(selected[0].source).toBe(pick.source);
			}
		});
	});
});

describe('invariants over every variants/*.json matrix', () => {
	it.each(variantCases.map((v) => [v.id, v] as const))('%s', (_id, c) => {
		const matrix = deepFreeze(structuredClone(c.expectedMatrix));
		const result = matchVariants({ matrix, picks: {}, preferences: [] });

		expect(result.isCustomMix).toBe(false);
		expect(result.compatibleVariantIds).toEqual(matrix.variants.map((v) => v.id));
		expect(Object.keys(result.picks)).toEqual(matrix.slots.map((s) => s.key));
		expect(matrix.variants.map((v) => v.id)).toContain(result.bestVariantId);
		for (const slot of matrix.slots) {
			expect(result.options[slot.key]).toHaveLength(slot.options.length);
			expect(result.options[slot.key].filter((o) => o.selected)).toHaveLength(1);
			// Auto picks are exactly the best variant's option in every slot.
			const own = slot.options.find((o) => o.variantIds.includes(result.bestVariantId));
			expect(result.picks[slot.key].optionKey).toBe(own?.key);
		}
		// Pinning each variant's own options reproduces that variant.
		for (const variant of matrix.variants) {
			let picks: Record<string, Pick> = {};
			for (const slot of matrix.slots) {
				const own = slot.options.find((o) => o.variantIds.includes(variant.id));
				if (own) picks = pin(picks, slot.key, own.key);
			}
			const again = matchVariants({ matrix, picks, preferences: [] });
			expect(again.compatibleVariantIds).toContain(variant.id);
			expect(again.isCustomMix).toBe(false);
		}
	});
});

describe('matchVariants edge cases', () => {
	const ingredient = (id: string): Ingredient => ({
		id,
		product: { id: `p-${id}` } as Ingredient['product'],
		quantity: null,
		baseAmount: null,
		note: null,
		optional: false
	});
	const variant = (id: string): VariantMatrix['variants'][number] => ({
		id,
		name: id,
		author: { id: 'u', nickname: 'u' },
		cookCount: 0,
		createdAt: '2026-01-01T00:00:00Z',
		isFavorite: false,
		productSwaps: []
	});

	it('returns the base and nothing else for an empty matrix', () => {
		const result = matchVariants({
			matrix: { recipeId: 'r', variants: [], slots: [] },
			picks: {},
			preferences: []
		});
		expect(result).toEqual({
			bestVariantId: 'base',
			isCustomMix: false,
			compatibleVariantIds: [],
			picks: {},
			options: {},
			ingredients: []
		});
	});

	it('falls back to the first option when no variant claims a slot', () => {
		const matrix: VariantMatrix = {
			recipeId: 'r',
			variants: [variant('base')],
			slots: [
				{
					key: 'i:x',
					label: 'x',
					options: [{ key: 'P:1', ingredient: ingredient('x'), variantIds: [] }]
				}
			]
		};
		const result = matchVariants({ matrix, picks: {}, preferences: [] });
		expect(result.picks['i:x']).toEqual({ optionKey: 'P:1', source: 'auto' });
	});

	it('sums duplicate preference entries', () => {
		const matrix: VariantMatrix = {
			recipeId: 'r',
			variants: [
				variant('base'),
				{ ...variant('v1'), productSwaps: [{ fromProductId: 'a', toProductId: 'b' }] },
				{ ...variant('v2'), productSwaps: [{ fromProductId: 'c', toProductId: 'd' }] }
			],
			slots: []
		};
		const preferences = [
			{ fromProductId: 'a', toProductId: 'b', weight: 1 },
			{ fromProductId: 'a', toProductId: 'b', weight: 1 },
			{ fromProductId: 'c', toProductId: 'd', weight: 1 }
		];
		expect(matchVariants({ matrix, picks: {}, preferences }).bestVariantId).toBe('v1');
	});

	it('does not mutate its inputs', () => {
		const c = matchCases.find((m) => m.id === 'F2-custom-mix-two-variants');
		if (!c) throw new Error('missing case');
		const matrix = deepFreeze(matrixOf(c));
		const picks = deepFreeze(structuredClone(c.picks));
		const preferences = deepFreeze(structuredClone(c.preferences));
		expect(() => matchVariants({ matrix, picks, preferences })).not.toThrow();
		expect(() => pin(picks, 'i:b1', 'P1:600')).not.toThrow();
		expect(() => clearUserPicks(picks)).not.toThrow();
	});
});

describe('pin and clearUserPicks', () => {
	const picks: Record<string, Pick> = deepFreeze({
		a: { optionKey: 'a1', source: 'auto' },
		b: { optionKey: 'b1', source: 'user' }
	});

	it('pin sets a user pick and overrides an auto one', () => {
		const next = pin(picks, 'a', 'a2');
		expect(next).toEqual({
			a: { optionKey: 'a2', source: 'user' },
			b: { optionKey: 'b1', source: 'user' }
		});
		expect(picks.a.source).toBe('auto');
	});

	it('pin adds a new slot', () => {
		expect(pin(picks, 'c', 'c1').c).toEqual({ optionKey: 'c1', source: 'user' });
	});

	it('clearUserPicks keeps only auto picks', () => {
		expect(clearUserPicks(picks)).toEqual({ a: { optionKey: 'a1', source: 'auto' } });
		expect(clearUserPicks({})).toEqual({});
	});
});

describe('draftFromMatch', () => {
	const ing = (id: string, productId: string): Ingredient => ({
		id,
		product: { id: productId } as Ingredient['product'],
		quantity: { value: 5, unit: 'g', size: null },
		baseAmount: 5,
		note: 'n',
		optional: true
	});
	const result: MatchResult = {
		bestVariantId: 'v',
		isCustomMix: false,
		compatibleVariantIds: ['v'],
		picks: {
			'i:b1': { optionKey: 'x', source: 'auto' },
			'p:pp': { optionKey: 'y', source: 'user' }
		},
		options: {},
		ingredients: [ing('b1', 'p1'), ing('added-1', 'pp')]
	};
	const source = {
		prepMinutes: 5,
		cookMinutes: null,
		utensils: [{ id: 'u1', name: 'garnek' }],
		prepSteps: [{ id: 's1', blocks: [{ type: 'text', text: 'Pokrój' }] }],
		instructionSteps: [{ id: 's2', blocks: [{ type: 'text', text: 'Gotuj' }] }]
	} as unknown as ResolvedRecipe;

	it('keeps base ids, drops ids of added rows and takes the rest from the source', () => {
		const frozenSource = deepFreeze(structuredClone(source));
		const draft = draftFromMatch(frozenSource, deepFreeze(structuredClone(result)));
		expect(draft.ingredients).toEqual([
			{
				id: 'b1',
				productId: 'p1',
				quantity: { value: 5, unit: 'g', size: null },
				note: 'n',
				optional: true
			},
			{
				productId: 'pp',
				quantity: { value: 5, unit: 'g', size: null },
				note: 'n',
				optional: true
			}
		]);
		expect(draft.ingredients[1]).not.toHaveProperty('id');
		expect(draft.utensilIds).toEqual(['u1']);
		expect(draft.prepMinutes).toBe(5);
		expect(draft.cookMinutes).toBeNull();
		expect(draft.prepSteps).toEqual([{ id: 's1', blocks: [{ type: 'text', text: 'Pokrój' }] }]);
		expect(draft.instructionSteps).toEqual([
			{ id: 's2', blocks: [{ type: 'text', text: 'Gotuj' }] }
		]);
	});
});

describe('performance', () => {
	it('matches 50 variants x 40 slots in well under 150 ms', () => {
		const variants = Array.from({ length: 50 }, (_, i) => ({
			id: i === 0 ? 'base' : `v${i}`,
			name: `v${i}`,
			author: { id: 'u', nickname: 'u' },
			cookCount: i % 7,
			createdAt: new Date(2026, 0, 1 + i).toISOString(),
			isFavorite: i % 11 === 0,
			productSwaps: [{ fromProductId: `p${i % 5}`, toProductId: `q${i % 3}` }]
		}));
		const slots = Array.from({ length: 40 }, (_, s) => {
			const options = new Map<string, string[]>();
			variants.forEach((v, i) => {
				const key = `P${s}:${(i * (s + 1)) % 4}`;
				options.set(key, [...(options.get(key) ?? []), v.id]);
			});
			return {
				key: `i:${s}`,
				label: `s${s}`,
				options: [...options].map(([key, variantIds]) => ({
					key,
					ingredient: null,
					variantIds
				}))
			};
		});
		const matrix = deepFreeze({ recipeId: 'r', variants, slots }) as unknown as VariantMatrix;
		const picks = deepFreeze({
			'i:3': { optionKey: slots[3].options[0].key, source: 'user' },
			'i:7': { optionKey: slots[7].options[0].key, source: 'auto' }
		} as Record<string, Pick>);

		matchVariants({ matrix, picks, preferences: [] }); // warm up
		const start = performance.now();
		const result = matchVariants({ matrix, picks, preferences: [] });
		const elapsed = performance.now() - start;

		expect(Object.keys(result.picks)).toHaveLength(40);
		expect(elapsed).toBeLessThan(150);
	});
});
