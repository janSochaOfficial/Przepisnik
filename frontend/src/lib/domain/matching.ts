// Client-side variant auto-match (PROJECT §6.3, §6.6). Implemented in F2.
import type {
	Ingredient,
	Pick,
	ProductPreference,
	ResolvedRecipe,
	SlotOption,
	VariantMatrix,
	VariantWrite
} from './types';

export type MatchInput = {
	matrix: VariantMatrix;
	picks: Record<string, Pick>;
	preferences: ProductPreference[];
};

export type RankedOption = {
	option: SlotOption;
	compatible: boolean;
	supportCount: number;
	selected: boolean;
	source?: 'user' | 'auto';
};

export type MatchResult = {
	/** A variant uuid or 'base'. */
	bestVariantId: string;
	isCustomMix: boolean;
	compatibleVariantIds: string[];
	/** Complete: user pins plus auto picks for every slot. */
	picks: Record<string, Pick>;
	/** Per slot, ordered for the option sheet. */
	options: Record<string, RankedOption[]>;
	/** Effective ingredient list in display order ("none" omitted). */
	ingredients: Ingredient[];
};

const BASE_SLOT_PREFIX = 'i:';
const NONE_KEY = 'none';

type MatrixVariantT = VariantMatrix['variants'][number];

type Scored = {
	variant: MatrixVariantT;
	index: number;
	/** Number of user pins this variant agrees with. */
	pinMatches: number;
	rememberedMatches: number;
	preferenceScore: number;
};

/** Cached lookup: slot key → variant id → option key. */
function indexMatrix(matrix: VariantMatrix): Map<string, Map<string, string>> {
	const bySlot = new Map<string, Map<string, string>>();
	for (const slot of matrix.slots) {
		const byVariant = new Map<string, string>();
		for (const option of slot.options) {
			for (const id of option.variantIds) byVariant.set(id, option.key);
		}
		bySlot.set(slot.key, byVariant);
	}
	return bySlot;
}

function preferenceWeights(preferences: ProductPreference[]): Map<string, number> {
	const weights = new Map<string, number>();
	for (const p of preferences) {
		const key = `${p.fromProductId}>${p.toProductId}`;
		weights.set(key, (weights.get(key) ?? 0) + p.weight);
	}
	return weights;
}

function byOlderFirst(a: MatrixVariantT, b: MatrixVariantT): number {
	return Date.parse(a.createdAt) - Date.parse(b.createdAt);
}

/** PROJECT §6.3 step 5: remembered matches, favourite, preference score, cook count, base first then older first. */
function compareRanking(a: Scored, b: Scored): number {
	return (
		b.rememberedMatches - a.rememberedMatches ||
		Number(b.variant.isFavorite) - Number(a.variant.isFavorite) ||
		b.preferenceScore - a.preferenceScore ||
		b.variant.cookCount - a.variant.cookCount ||
		Number(b.variant.id === 'base') - Number(a.variant.id === 'base') ||
		byOlderFirst(a.variant, b.variant) ||
		a.index - b.index
	);
}

export function matchVariants(input: MatchInput): MatchResult {
	const { matrix, preferences } = input;
	const index = indexMatrix(matrix);
	const weights = preferenceWeights(preferences);

	// Drop picks that point at a slot or option that no longer exists (a variant was deleted).
	const validPicks: Record<string, Pick> = {};
	for (const [slotKey, pick] of Object.entries(input.picks)) {
		const slot = matrix.slots.find((s) => s.key === slotKey);
		if (slot?.options.some((o) => o.key === pick.optionKey)) validPicks[slotKey] = pick;
	}
	const userPins = Object.entries(validPicks).filter(([, p]) => p.source === 'user');

	const scored: Scored[] = matrix.variants.map((variant, i) => {
		const optionOf = (slotKey: string) => index.get(slotKey)?.get(variant.id);
		return {
			variant,
			index: i,
			pinMatches: userPins.filter(([slotKey, p]) => optionOf(slotKey) === p.optionKey).length,
			rememberedMatches: Object.entries(validPicks).filter(
				([slotKey, p]) => optionOf(slotKey) === p.optionKey
			).length,
			preferenceScore: variant.productSwaps.reduce(
				(sum, s) => sum + (weights.get(`${s.fromProductId}>${s.toProductId}`) ?? 0),
				0
			)
		};
	});

	const compatible = scored.filter((s) => s.pinMatches === userPins.length);
	const isCustomMix = compatible.length === 0 && scored.length > 0;
	let best: Scored | undefined;
	if (isCustomMix) {
		// No single variant holds every pin: take the one that matches the most pins (same tie-breakers).
		best = [...scored].sort((a, b) => b.pinMatches - a.pinMatches || compareRanking(a, b))[0];
	} else {
		best = [...compatible].sort(compareRanking)[0];
	}
	const bestVariantId = best?.variant.id ?? 'base';
	const compatibleIds = new Set(compatible.map((s) => s.variant.id));

	const picks: Record<string, Pick> = {};
	const options: Record<string, RankedOption[]> = {};
	for (const slot of matrix.slots) {
		const pinned = validPicks[slot.key]?.source === 'user' ? validPicks[slot.key] : undefined;
		const optionKey =
			pinned?.optionKey ??
			index.get(slot.key)?.get(bestVariantId) ??
			validPicks[slot.key]?.optionKey ??
			slot.options[0].key;
		picks[slot.key] = pinned ?? { optionKey, source: 'auto' };

		const ranked = slot.options.map((option, position) => ({
			position,
			ranked: {
				option,
				compatible: option.variantIds.some((id) => compatibleIds.has(id)),
				supportCount: option.variantIds.length,
				selected: option.key === optionKey,
				...(option.key === optionKey ? { source: picks[slot.key].source } : {})
			} satisfies RankedOption
		}));
		ranked.sort(
			(a, b) =>
				Number(b.ranked.compatible) - Number(a.ranked.compatible) ||
				b.ranked.supportCount - a.ranked.supportCount ||
				a.position - b.position
		);
		options[slot.key] = ranked.map((r) => r.ranked);
	}

	// Base slots keep the base order. The matrix carries no "after which row" data for added slots
	// (API §5.1), so they follow the base rows in first-seen order.
	const ordered = [
		...matrix.slots.filter((s) => s.key.startsWith(BASE_SLOT_PREFIX)),
		...matrix.slots.filter((s) => !s.key.startsWith(BASE_SLOT_PREFIX))
	];
	const ingredients: Ingredient[] = [];
	for (const slot of ordered) {
		const chosen = slot.options.find((o) => o.key === picks[slot.key].optionKey);
		if (chosen && chosen.key !== NONE_KEY && chosen.ingredient) ingredients.push(chosen.ingredient);
	}

	return {
		bestVariantId,
		isCustomMix,
		compatibleVariantIds: compatible.map((s) => s.variant.id),
		picks,
		options,
		ingredients
	};
}

/** Pins an option as a user pick; returns a new map. */
export function pin(
	picks: Record<string, Pick>,
	slotKey: string,
	optionKey: string
): Record<string, Pick> {
	return { ...picks, [slotKey]: { optionKey, source: 'user' } };
}

/** Removes every user pick (PROJECT §6.3 step 11); auto picks stay as soft memory. */
export function clearUserPicks(picks: Record<string, Pick>): Record<string, Pick> {
	return Object.fromEntries(Object.entries(picks).filter(([, p]) => p.source !== 'user'));
}

/**
 * Prefilled variant-editor payload: matched ingredients (base ids kept for base slots, none for added
 * ones) with steps, utensils and times from `stepsSource` (the best variant, resolved).
 */
export function draftFromMatch(
	stepsSource: ResolvedRecipe,
	result: MatchResult
): VariantWrite['recipe'] {
	const baseIds = new Set(
		Object.keys(result.picks)
			.filter((k) => k.startsWith(BASE_SLOT_PREFIX))
			.map((k) => k.slice(BASE_SLOT_PREFIX.length))
	);
	return {
		prepMinutes: stepsSource.prepMinutes,
		cookMinutes: stepsSource.cookMinutes,
		utensilIds: stepsSource.utensils.map((u) => u.id),
		ingredients: result.ingredients.map((i) => ({
			...(baseIds.has(i.id) ? { id: i.id } : {}),
			productId: i.product.id,
			quantity: i.quantity,
			note: i.note,
			optional: i.optional
		})),
		prepSteps: stepsSource.prepSteps.map((s) => ({ id: s.id, blocks: structuredClone(s.blocks) })),
		instructionSteps: stepsSource.instructionSteps.map((s) => ({
			id: s.id,
			blocks: structuredClone(s.blocks)
		}))
	};
}
