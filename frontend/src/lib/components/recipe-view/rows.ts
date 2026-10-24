// Pure helpers for the recipe view: slot order and the rows of the ingredient list.
import type { Ingredient, Pick, Slot, SlotOption, VariantMatrix } from '$lib/domain/types';

const BASE_PREFIX = 'i:';
export const NONE_KEY = 'none';

export type SlotRow = {
	slotKey: string;
	slot: Slot;
	/** The chosen option; null when the pick points at nothing known. */
	option: SlotOption | null;
	/** Null for "none" (the ingredient is absent in the current selection). */
	ingredient: Ingredient | null;
	pick: Pick;
	isBase: boolean;
	/** The chosen option is not what the base recipe has in this slot. */
	changed: boolean;
};

/**
 * Base slots in base order; added slots after the base slot named by `afterSlotKey` (B4 contract),
 * `null` at the start, and, when the field is absent (older fixtures), after all base rows.
 */
export function orderSlots(slots: Slot[]): Slot[] {
	const isBase = (s: Slot) => s.key.startsWith(BASE_PREFIX);
	const baseSlots = slots.filter(isBase);
	const baseKeys = new Set(baseSlots.map((s) => s.key));
	const head: Slot[] = [];
	const tail: Slot[] = [];
	const after = new Map<string, Slot[]>();
	for (const slot of slots.filter((s) => !isBase(s))) {
		if (slot.afterSlotKey === null) head.push(slot);
		else if (slot.afterSlotKey !== undefined && baseKeys.has(slot.afterSlotKey)) {
			const list = after.get(slot.afterSlotKey) ?? [];
			list.push(slot);
			after.set(slot.afterSlotKey, list);
		} else tail.push(slot);
	}
	return [...head, ...baseSlots.flatMap((s) => [s, ...(after.get(s.key) ?? [])]), ...tail];
}

/** One row per slot, in display order, for the complete `picks` of a match result. */
export function buildRows(matrix: VariantMatrix, picks: Record<string, Pick>): SlotRow[] {
	return orderSlots(matrix.slots).map((slot) => {
		const pick = picks[slot.key] ?? { optionKey: NONE_KEY, source: 'auto' as const };
		const option = slot.options.find((o) => o.key === pick.optionKey) ?? null;
		const ingredient: Ingredient | null =
			option && option.key !== NONE_KEY ? option.ingredient : null;
		return {
			slotKey: slot.key,
			slot,
			option,
			ingredient,
			pick,
			isBase: slot.key.startsWith(BASE_PREFIX),
			changed: !(option?.variantIds.includes('base') ?? false)
		};
	});
}
