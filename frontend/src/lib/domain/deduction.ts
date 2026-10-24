// Planning a deduction (PROJECT §9): what a recipe needs vs. what a fridge holds. Pure; amounts are
// base units (g, ml, medium pieces), the same as the API.
import type { FridgeItem, Ingredient, ProductRef } from './types';

/** An ingredient reduced to what the plan needs: "do smaku" and unconvertible rows have `amount: null`. */
export type NeededIngredient = { product: ProductRef; amount: number | null };

export type DeductionStatus = 'ok' | 'short' | 'missing';

export type DeductionRow = {
	product: ProductRef;
	/** Total needed in this recipe (the same product on several lines is summed). */
	needed: number;
	/** Total remaining over all packages of the product in the fridge. */
	available: number;
	status: DeductionStatus;
	/** Whether the row's switch starts on: anything that is at least partly in the fridge. */
	defaultOn: boolean;
};

export type DeductionRequest = {
	recipeId?: string;
	variantId?: string;
	lines: { productId: string; amount: number }[];
};

/** Tolerance for floating point noise when comparing amounts. */
const EPS = 1e-6;

/** The ingredients of a resolved recipe at `factor`, ready for {@link planDeduction}. */
export function neededFromIngredients(ingredients: Ingredient[], factor = 1): NeededIngredient[] {
	return ingredients.map((i) => ({
		product: i.product,
		amount: i.baseAmount === null ? null : i.baseAmount * factor
	}));
}

/**
 * One row per product that has an amount, in the order of first appearance. Several packages are
 * summed; "do smaku" (no amount) rows are left out.
 */
export function planDeduction(
	ingredients: NeededIngredient[],
	fridgeItems: Pick<FridgeItem, 'product' | 'remainingAmount'>[]
): DeductionRow[] {
	const needed = new Map<string, { product: ProductRef; amount: number }>();
	for (const { product, amount } of ingredients) {
		if (amount === null || !(amount > 0)) continue;
		const row = needed.get(product.id);
		if (row) row.amount += amount;
		else needed.set(product.id, { product, amount });
	}

	const available = new Map<string, number>();
	for (const item of fridgeItems) {
		available.set(item.product.id, (available.get(item.product.id) ?? 0) + item.remainingAmount);
	}

	return [...needed.values()].map(({ product, amount }) => {
		const have = available.get(product.id) ?? 0;
		const status: DeductionStatus = have <= EPS ? 'missing' : have + EPS < amount ? 'short' : 'ok';
		return {
			product,
			needed: amount,
			available: status === 'missing' ? 0 : have,
			status,
			defaultOn: status !== 'missing'
		};
	});
}

/** The rows whose switch starts on, by product id. */
export function defaultSelection(rows: DeductionRow[]): Set<string> {
	return new Set(rows.filter((r) => r.defaultOn).map((r) => r.product.id));
}

/** Every row that has something in the fridge ("Odejmij wszystko"). */
export function allAvailable(rows: DeductionRow[]): Set<string> {
	return new Set(rows.filter((r) => r.status !== 'missing').map((r) => r.product.id));
}

/**
 * The `POST /fridges/{id}/deductions` body for the selected rows. Missing rows are never sent (the
 * server would answer with a shortfall only); a short row asks for the full need and the server
 * deducts what is there.
 */
export function toDeductionRequest(
	rows: DeductionRow[],
	selection: ReadonlySet<string>,
	source: { recipeId?: string | null; variantId?: string | null } = {}
): DeductionRequest {
	const request: DeductionRequest = {
		lines: rows
			.filter((r) => selection.has(r.product.id) && r.status !== 'missing')
			.map((r) => ({ productId: r.product.id, amount: r.needed }))
	};
	if (source.recipeId) {
		request.recipeId = source.recipeId;
		if (source.variantId) request.variantId = source.variantId;
	}
	return request;
}

/** What to buy: the whole need of missing rows and the shortfall of short rows. */
export function toShoppingLines(rows: DeductionRow[]): { productId: string; amount: number }[] {
	return rows.flatMap((r) => {
		const amount = r.status === 'missing' ? r.needed : r.needed - r.available;
		return r.status === 'ok' || !(amount > EPS) ? [] : [{ productId: r.product.id, amount }];
	});
}
