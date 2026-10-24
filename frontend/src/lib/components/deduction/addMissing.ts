import { mount, unmount } from 'svelte';
import { createApi, unwrap } from '$lib/api/client';
import { toast } from '$lib/components/ui';
import {
	missingOptionalRows,
	neededFromIngredients,
	planDeduction,
	toShoppingLines,
	type DeductionRow
} from '$lib/domain/deduction';
import type { Ingredient } from '$lib/domain/types';
import { t } from '$lib/i18n';
import OptionalPrompt from './OptionalPrompt.svelte';

/** Asks about the optional names; resolves `true` (include), `false` (required only) or `null` (cancelled). */
function askOptional(names: string): Promise<boolean | null> {
	return new Promise((resolve) => {
		const component = mount(OptionalPrompt, {
			target: document.body,
			props: {
				names,
				onAnswer: (answer) => {
					resolve(answer);
					// Let the dialog finish closing before it is removed.
					setTimeout(() => void unmount(component), 300);
				}
			}
		});
	});
}

/**
 * The single path of "add missing to the shopping list" (dialog button and action bar): adds
 * directly when no optional line is missing, otherwise asks first. Shows the toast; returns the
 * number of lines added, or `null` when the user cancelled the question.
 */
export async function addRowsToShoppingList(
	fridgeId: string,
	rows: DeductionRow[]
): Promise<number | null> {
	const optionalRows = missingOptionalRows(rows);
	let includeOptional = true;
	if (optionalRows.length > 0) {
		const answer = await askOptional(optionalRows.map((r) => r.product.name).join(', '));
		if (answer === null) return null;
		includeOptional = answer;
	}
	const lines = toShoppingLines(rows, { includeOptional });
	if (lines.length === 0) {
		toast.show(t.shopping.nothingMissing);
		return 0;
	}
	await unwrap(
		createApi().POST('/fridges/{id}/shopping-list/items', {
			params: { path: { id: fridgeId } },
			body: { items: lines.slice(0, 100) }
		})
	);
	const added = Math.min(lines.length, 100);
	toast.success(t.shopping.addedMissing(added));
	return added;
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
		await addRowsToShoppingList(fridgeId, rows);
	} catch {
		toast.error(t.shopping.addMissingFailed);
	}
}
