// Seeds for the variant editor (PROJECT §6.2): the base recipe as an editable copy, with the
// variant's (or the current selection's) values applied and base rows removed in it marked `removed`.
import {
	draftFromRecipe,
	type DraftIngredient,
	type DraftStep,
	type RecipeDraft,
	type RecipeLike
} from '$lib/components/recipe-editor/draft';
import type { Ingredient } from '$lib/domain/types';
import type { SlotRow } from './rows';

/**
 * Merges `source` rows with the base rows: base rows keep `base: true` (and their id), rows the base
 * does not have become new rows (no id), and base rows missing from `source` come back as `removed`.
 */
function merge<T extends { id?: string; base: boolean; removed: boolean }>(
	baseRows: T[],
	sourceRows: T[]
): T[] {
	const baseIds = new Set(baseRows.map((r) => r.id));
	const sourceIds = new Set(sourceRows.map((r) => r.id));
	const mark = (r: T): T =>
		r.id !== undefined && baseIds.has(r.id)
			? { ...r, base: true }
			: { ...r, id: undefined, base: false };
	const out: T[] = [];
	// Removed base rows wait until the next base row that survives, so rows the variant added in
	// between come first (they follow an earlier base row in the source).
	let pending: T[] = [];
	let next = 0;
	for (const b of baseRows) {
		if (!sourceIds.has(b.id)) {
			pending.push({ ...b, base: true, removed: true });
			continue;
		}
		const before: T[] = [];
		while (next < sourceRows.length) {
			const row = sourceRows[next++] as T;
			if (row.id === b.id) {
				out.push(...before.map(mark), ...pending, mark(row));
				pending = [];
				break;
			}
			before.push(row);
		}
	}
	while (next < sourceRows.length) out.push(mark(sourceRows[next++] as T));
	out.push(...pending);
	return out;
}

/** `like` is a resolved variant (or the steps source of a selection); `base` the current base recipe. */
export function variantDraft(base: RecipeLike, like: RecipeLike): RecipeDraft {
	const baseDraft = draftFromRecipe(base, { base: true });
	const draft = draftFromRecipe(like);
	return {
		...draft,
		ingredients: merge<DraftIngredient>(baseDraft.ingredients, draft.ingredients),
		prepSteps: merge<DraftStep>(baseDraft.prepSteps, draft.prepSteps),
		instructionSteps: merge<DraftStep>(baseDraft.instructionSteps, draft.instructionSteps)
	};
}

/** The editor draft for "create a variant from the current selection". */
export function draftFromSelection(
	base: RecipeLike,
	stepsSource: RecipeLike,
	rows: SlotRow[]
): RecipeDraft {
	const ingredients: Ingredient[] = rows.flatMap((r) =>
		r.ingredient ? [r.isBase ? { ...r.ingredient, id: r.slotKey.slice(2) } : r.ingredient] : []
	);
	return variantDraft(base, { ...stepsSource, ingredients });
}
