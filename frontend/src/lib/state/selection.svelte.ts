// The state of the recipe view (PROJECT §6.3, §6.6): picks, scaling, the derived match and the
// debounced `PUT my-selection`. One instance per opened recipe.
import { createApi, unwrap } from '$lib/api/client';
import { buildRows, type SlotRow } from '$lib/components/recipe-view/rows';
import { toast } from '$lib/components/ui';
import { clearUserPicks, matchVariants, pin, type MatchResult } from '$lib/domain/matching';
import {
	clampServings,
	scaledServings,
	scaleFactor,
	scaleIngredients,
	servingsFactor,
	type ScaledServings
} from '$lib/domain/scaling';
import type {
	Ingredient,
	LiquidUnits,
	Pick,
	ProductPreference,
	RecipeDetail,
	ResolvedRecipe,
	ScalingState,
	Selection,
	VariantMatrix
} from '$lib/domain/types';
import { t } from '$lib/i18n';

export const SAVE_DELAY_MS = 1000;

export type SelectionInit = {
	recipe: RecipeDetail;
	matrix: VariantMatrix;
	/** The saved selection, or null (204). */
	selection: Selection | null;
	preferences: ProductPreference[];
	liquidUnits: LiquidUnits;
	/** `?wariant=`: pins every slot to this variant's options, replacing what is saved. */
	pinVariantId?: string | null;
};

/** Pins every slot to the option `variantId` has in it; unknown ids pin nothing. */
export function picksOfVariant(matrix: VariantMatrix, variantId: string): Record<string, Pick> {
	if (!matrix.variants.some((v) => v.id === variantId)) return {};
	const picks: Record<string, Pick> = {};
	for (const slot of matrix.slots) {
		const option = slot.options.find((o) => o.variantIds.some((id) => id === variantId));
		if (option) picks[slot.key] = { optionKey: option.key, source: 'user' };
	}
	return picks;
}

export class SelectionState {
	recipe = $state.raw<RecipeDetail>() as RecipeDetail;
	matrix = $state.raw<VariantMatrix>() as VariantMatrix;
	preferences = $state.raw<ProductPreference[]>([]);
	liquidUnits = $state.raw<LiquidUnits>('kitchen');
	/** User pins plus remembered auto picks (what the server stores). */
	picks = $state.raw<Record<string, Pick>>({});
	scaling = $state.raw<ScalingState | null>(null);

	/** Resolved variants fetched so far, by variant id. */
	resolved = $state.raw<Record<string, ResolvedRecipe>>({});
	/** The last fetched variant, shown while the next one loads. */
	lastResolved = $state.raw<ResolvedRecipe | null>(null);
	stepsError = $state.raw<unknown>(null);

	private timer: ReturnType<typeof setTimeout> | null = null;

	match: MatchResult = $derived.by(() =>
		matchVariants({ matrix: this.matrix, picks: this.picks, preferences: this.preferences })
	);

	rows: SlotRow[] = $derived(buildRows(this.matrix, this.match.picks));

	/** Rows that have an ingredient in the current selection. */
	present: SlotRow[] = $derived(this.rows.filter((r) => r.ingredient !== null));

	hasUserPins = $derived(Object.values(this.picks).some((p) => p.source === 'user'));

	/**
	 * The servings of what is shown (the steps source: the best variant's override or the recipe's),
	 * or null when it states none (PROJECT §5.7).
	 */
	servings: number | null = $derived.by(() => this.stepsSource.servings ?? null);

	/**
	 * The scaling factor: chosen / shown servings for a servings choice (1 without servings), entered
	 * / recipe amount for an ingredient anchor (1 when the anchor row has no amount).
	 */
	factor = $derived.by(() => {
		const scaling = this.scaling;
		if (!scaling) return 1;
		if (scaling.kind === 'servings') return servingsFactor(this.servings, scaling.servings);
		const row = this.rows.find((r) => r.slotKey === scaling.anchorSlotKey);
		const base = row?.ingredient?.baseAmount ?? null;
		return base === null ? 1 : scaleFactor(base, scaling.anchorBaseAmount);
	});

	/**
	 * The servings line: the chosen number, the shown servings (unscaled), or `servings × factor`
	 * rounded to ½ when scaled from an ingredient. Null when the shown recipe has no servings.
	 */
	shownServings: ScaledServings | null = $derived.by(() => {
		const servings = this.servings;
		if (servings === null) return null;
		const scaling = this.scaling;
		if (scaling?.kind === 'servings') return { value: scaling.servings, approximate: false };
		if (!scaling || this.factor === 1) return { value: servings, approximate: false };
		return scaledServings(servings, this.factor);
	});

	/** The effective, scaled ingredient list (consumed by F6 through `CookContext`). */
	cookIngredients = $derived(
		scaleIngredients(
			this.present.map((r) => r.ingredient as Ingredient),
			this.factor
		)
	);

	/** The variant whose steps, utensils and times are shown. */
	stepsSource = $derived.by(() => {
		const id = this.match.bestVariantId;
		if (id === 'base') return this.recipe;
		return this.resolved[id] ?? this.lastResolved ?? this.recipe;
	});

	stepsLoading = $derived(
		this.match.bestVariantId !== 'base' && !this.resolved[this.match.bestVariantId]
	);

	bestVariant = $derived(this.matrix.variants.find((v) => v.id === this.match.bestVariantId));

	constructor(init: SelectionInit) {
		this.recipe = init.recipe;
		this.matrix = init.matrix;
		this.preferences = init.preferences;
		this.liquidUnits = init.liquidUnits;
		if (init.selection) {
			this.picks = init.selection.picks;
			this.scaling = init.selection.scaling ?? null;
		}
		// `?wariant=` beats whatever is remembered (D-38); an unknown id keeps the remembered state.
		if (init.pinVariantId) this.openVariant(init.pinVariantId);
	}

	/**
	 * Shows exactly `variantId` ('base' for the base recipe): pins every slot to its option and
	 * replaces the remembered picks. An ingredient anchor is kept when its slot still has an amount;
	 * a servings choice is always kept (it is about people, not a slot) and its factor follows the
	 * new variant's servings. Returns false (and changes nothing) for an id the matrix does not know.
	 */
	openVariant(variantId: string): boolean {
		const picks = picksOfVariant(this.matrix, variantId);
		if (Object.keys(picks).length === 0) return false;
		this.picks = picks;
		const anchor = this.scaling;
		if (anchor && anchor.kind !== 'servings') {
			const slot = this.matrix.slots.find((s) => s.key === anchor.anchorSlotKey);
			const option = slot?.options.find((o) => o.key === picks[anchor.anchorSlotKey]?.optionKey);
			if ((option?.ingredient?.baseAmount ?? null) === null) this.scaling = null;
		}
		this.scheduleSave();
		return true;
	}

	/** Fetches `GET /variants/{id}/resolved` once per variant. */
	async ensureResolved(variantId: string): Promise<void> {
		if (variantId === 'base' || this.resolved[variantId]) return;
		try {
			const r = await unwrap(
				createApi().GET('/variants/{id}/resolved', { params: { path: { id: variantId } } })
			);
			this.resolved = { ...this.resolved, [variantId]: r };
			this.lastResolved = r;
			this.stepsError = null;
		} catch (error) {
			this.stepsError = error;
		}
	}

	pick(slotKey: string, optionKey: string): void {
		this.picks = pin(this.picks, slotKey, optionKey);
		this.scheduleSave();
	}

	clearPins(): void {
		this.picks = clearUserPicks(this.picks);
		this.scheduleSave();
	}

	/** "Mam: …" on an ingredient; replaces a servings choice (one scaling choice, PROJECT §5.7). */
	setScaling(slotKey: string, anchorBaseAmount: number): void {
		this.scaling = { anchorSlotKey: slotKey, anchorBaseAmount };
		this.scheduleSave();
	}

	/** Chooses a number of servings (clamped to 1–100); replaces an ingredient anchor. */
	setServings(servings: number): void {
		this.scaling = { kind: 'servings', servings: clampServings(servings) };
		this.scheduleSave();
	}

	resetScaling(): void {
		this.scaling = null;
		this.scheduleSave();
	}

	/**
	 * Re-reads the matrix after a variant was deleted: the Warianty list and count follow, and remembered
	 * picks of options that no longer exist are dropped (the server drops them too).
	 */
	async reloadMatrix(): Promise<void> {
		const id = this.recipe.id;
		const matrix = await unwrap(
			createApi().GET('/recipes/{id}/variant-matrix', { params: { path: { id } } })
		);
		const picks: Record<string, Pick> = {};
		for (const [slotKey, pick] of Object.entries(this.picks)) {
			const slot = matrix.slots.find((s) => s.key === slotKey);
			if (slot?.options.some((o) => o.key === pick.optionKey)) picks[slotKey] = pick;
		}
		this.matrix = matrix;
		this.picks = picks;
		this.recipe = {
			...this.recipe,
			variantCount: matrix.variants.filter((v) => v.id !== 'base').length
		};
	}

	/** Keeps the matrix in step with a ♥ toggled on a variant ('base' is the recipe's own ♥). */
	setFavorite(variantId: string, favorite: boolean): void {
		this.matrix = {
			...this.matrix,
			variants: this.matrix.variants.map((v) =>
				v.id === variantId ? { ...v, isFavorite: favorite } : v
			)
		};
		if (variantId === 'base') this.recipe = { ...this.recipe, isFavorite: favorite };
	}

	/** Saves one second after the last change (PROJECT §6.6). */
	scheduleSave(): void {
		if (this.timer) clearTimeout(this.timer);
		this.timer = setTimeout(() => void this.save(), SAVE_DELAY_MS);
	}

	/** Sends a pending save now (leaving the page). */
	flush(): void {
		if (this.timer) void this.save();
	}

	private async save(): Promise<void> {
		if (this.timer) clearTimeout(this.timer);
		this.timer = null;
		try {
			await unwrap(
				createApi().PUT('/recipes/{id}/my-selection', {
					params: { path: { id: this.recipe.id } },
					body: { picks: this.match.picks, ...(this.scaling ? { scaling: this.scaling } : {}) }
				})
			);
		} catch {
			toast.error(t.variants.selectionSaveFailed);
		}
	}
}
