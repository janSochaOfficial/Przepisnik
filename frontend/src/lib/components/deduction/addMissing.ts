import { createApi, unwrap } from '$lib/api/client';
import { toast } from '$lib/components/ui';
import {
	neededFromIngredients,
	planDeduction,
	toShoppingLines,
	type DeductionRow
} from '$lib/domain/deduction';
import type { Ingredient } from '$lib/domain/types';
import { t } from '$lib/i18n';

/** Adds the shortfalls and missing products of `rows` to the shopping list of `fridgeId`; returns how many. */
export async function addRowsToShoppingList(
	fridgeId: string,
	rows: DeductionRow[]
): Promise<number> {
	const lines = toShoppingLines(rows);
	if (lines.length === 0) return 0;
	await unwrap(
		createApi().POST('/fridges/{id}/shopping-list/items', {
			params: { path: { id: fridgeId } },
			body: { items: lines.slice(0, 100) }
		})
	);
	return lines.length;
}

/**
 * "Add missing to the shopping list" without opening the dialog (recipe action bar): compares the
 * recipe at `factor` with the fridge and adds the missing and short amounts to that fridge's list,
 * then shows a toast. Without `fridgeId` the user's oldest fridge is used.
 */
export async function addMissingToShoppingList(options: {
	ingredients: Ingredient[];
	factor: number;
	fridgeId?: string | null;
}): Promise<void> {
	const api = createApi();
	try {
		const fridgeId = options.fridgeId ?? (await unwrap(api.GET('/fridges')))[0]?.id;
		if (!fridgeId) {
			toast.error(t.shopping.addMissingFailed);
			return;
		}
		const items = await unwrap(
			api.GET('/fridges/{id}/items', { params: { path: { id: fridgeId } } })
		);
		const rows = planDeduction(neededFromIngredients(options.ingredients, options.factor), items);
		const added = await addRowsToShoppingList(fridgeId, rows);
		if (added === 0) toast.show(t.shopping.nothingMissing);
		else toast.success(t.shopping.addedMissing(added));
	} catch {
		toast.error(t.shopping.addMissingFailed);
	}
}
