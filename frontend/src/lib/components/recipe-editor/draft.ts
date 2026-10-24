// The editor's working copy of a recipe, its conversion to and from the API shapes, client-side
// validation (mirroring RecipeInputValidator) and the mapping of server field errors back to rows.
import type { components } from '$lib/api/schema';
import { t } from '$lib/i18n';
import { toBaseAmount } from '$lib/domain/units';
import type {
	BuiltInUnit,
	CustomUnitRef,
	PieceSize,
	ProductRef,
	Quantity,
	Unit
} from '$lib/domain/types';
import { fromContentBlocks, newKey, textBlock, toContentBlocks, type DraftBlock } from './blocks';

type Schemas = components['schemas'];
type ImageInfo = Schemas['ImageInfo'];

export const LIMITS = {
	title: 150,
	description: 2000,
	minutes: 2880,
	ingredients: 100,
	steps: 50,
	note: 300,
	text: 5000,
	caption: 300
} as const;

export type DraftIngredient = {
	key: string;
	/** Existing id; kept on edit so variant operations stay valid. */
	id?: string;
	product: ProductRef | null;
	value: number | null;
	unit: Unit | null;
	/** The custom unit when `unit` is `custom` (PROJECT §5.6). */
	customUnit: CustomUnitRef | null;
	size: PieceSize | null;
	toTaste: boolean;
	note: string;
	optional: boolean;
	/** Variant mode: a row of the base recipe (cannot be reordered). */
	base: boolean;
	/** Variant mode: a base row removed in this variant. */
	removed: boolean;
};

export type DraftStep = {
	key: string;
	id?: string;
	blocks: DraftBlock[];
	base: boolean;
	removed: boolean;
};

export type RecipeDraft = {
	title: string;
	description: string;
	cover: ImageInfo | null;
	prepMinutes: number | null;
	cookMinutes: number | null;
	tags: string[];
	utensils: string[];
	ingredients: DraftIngredient[];
	prepSteps: DraftStep[];
	instructionSteps: DraftStep[];
};

export type EditorMode = 'recipe' | 'variant';

/** Built-in units in the order they appear in the unit select. */
export const UNITS: readonly BuiltInUnit[] = [
	'g',
	'kg',
	'ml',
	'l',
	'glass',
	'tbsp',
	'tsp',
	'drop',
	'pinch',
	'piece'
];

/** Units that convert to the product's base kind (F1 `toBaseAmount` ≠ null). */
export function compatibleUnits(product: ProductRef | null): BuiltInUnit[] {
	if (!product) return [...UNITS];
	return UNITS.filter((unit) => toBaseAmount({ value: 1, unit }, product) !== null);
}

/** The unit a fresh row of `product` starts with. */
export function defaultUnit(product: ProductRef): BuiltInUnit {
	const units = compatibleUnits(product);
	const natural: BuiltInUnit =
		product.measureKind === 'mass' ? 'g' : product.measureKind === 'volume' ? 'ml' : 'piece';
	return units.includes(natural) ? natural : (units[0] ?? 'g');
}

export function emptyIngredient(base = false): DraftIngredient {
	return {
		key: newKey('i'),
		product: null,
		value: null,
		unit: null,
		customUnit: null,
		size: null,
		toTaste: false,
		note: '',
		optional: false,
		base,
		removed: false
	};
}

export function emptyStep(): DraftStep {
	return { key: newKey('s'), blocks: [textBlock()], base: false, removed: false };
}

export function emptyDraft(): RecipeDraft {
	return {
		title: '',
		description: '',
		cover: null,
		prepMinutes: null,
		cookMinutes: null,
		tags: [],
		utensils: [],
		ingredients: [emptyIngredient()],
		prepSteps: [],
		instructionSteps: [emptyStep()]
	};
}

/** The part of `RecipeDetail` / `ResolvedRecipe` the editor needs. */
export type RecipeLike = Pick<
	Schemas['RecipeDetail'],
	| 'title'
	| 'description'
	| 'coverImage'
	| 'prepMinutes'
	| 'cookMinutes'
	| 'tags'
	| 'utensils'
	| 'ingredients'
	| 'prepSteps'
	| 'instructionSteps'
>;

/** `base: true` (variant mode) marks every row as belonging to the base recipe. */
export function draftFromRecipe(recipe: RecipeLike, options: { base?: boolean } = {}): RecipeDraft {
	const base = options.base ?? false;
	const step = (s: Schemas['Step']): DraftStep => ({
		key: newKey('s'),
		id: s.id,
		blocks: fromContentBlocks(s.blocks),
		base,
		removed: false
	});
	return {
		title: recipe.title,
		description: recipe.description ?? '',
		cover: recipe.coverImage,
		prepMinutes: recipe.prepMinutes,
		cookMinutes: recipe.cookMinutes,
		tags: recipe.tags.filter((tag) => !tag.isSystem).map((tag) => tag.name),
		utensils: recipe.utensils.map((u) => u.name),
		ingredients: recipe.ingredients.map((i) => ({
			key: newKey('i'),
			id: i.id,
			product: i.product,
			value: i.quantity?.value ?? null,
			unit: i.quantity?.unit ?? null,
			customUnit: i.quantity?.customUnit ?? null,
			size: i.quantity?.size ?? null,
			toTaste: i.quantity === null,
			note: i.note ?? '',
			optional: i.optional,
			base,
			removed: false
		})),
		prepSteps: recipe.prepSteps.map(step),
		instructionSteps: recipe.instructionSteps.map(step)
	};
}

/** Maps a payload index back to the editor row it came from (rows removed in a variant are skipped). */
export type RowMaps = {
	ingredients: number[];
	prepSteps: number[];
	instructionSteps: number[];
};

export type BuiltBody = {
	body: Omit<Schemas['RecipeWrite'], 'tagIds' | 'utensilIds'>;
	maps: RowMaps;
};

/** The quantity of a row; a custom unit is sent by id only (`customUnit` is read-only in the API). */
function quantityOf(i: DraftIngredient): Quantity | undefined {
	if (i.toTaste || i.value === null || i.unit === null) return undefined;
	if (i.unit === 'custom') {
		return i.customUnit
			? { value: i.value, unit: 'custom', customUnitId: i.customUnit.id }
			: undefined;
	}
	const q: Quantity = { value: i.value, unit: i.unit };
	if (i.unit === 'piece' && i.product?.hasSizes && i.size) q.size = i.size;
	return q;
}

/** The recipe part of a write request, without tag and utensil ids (the caller resolves names). */
export function buildBody(draft: RecipeDraft): BuiltBody {
	const maps: RowMaps = { ingredients: [], prepSteps: [], instructionSteps: [] };
	const ingredients: Schemas['IngredientWrite'][] = [];
	draft.ingredients.forEach((i, index) => {
		if (i.removed || !i.product) return;
		maps.ingredients.push(index);
		const note = i.note.trim();
		const row: Schemas['IngredientWrite'] = {
			...(i.id ? { id: i.id } : {}),
			productId: i.product.id,
			...(quantityOf(i) ? { quantity: quantityOf(i) } : {}),
			...(note ? { note } : {}),
			optional: i.optional
		};
		ingredients.push(row);
	});
	const steps = (key: 'prepSteps' | 'instructionSteps'): Schemas['StepWrite'][] => {
		const out: Schemas['StepWrite'][] = [];
		draft[key].forEach((s, index) => {
			if (s.removed) return;
			maps[key].push(index);
			out.push({ ...(s.id ? { id: s.id } : {}), blocks: toContentBlocks(s.blocks) });
		});
		return out;
	};
	const description = draft.description.trim();
	return {
		body: {
			title: draft.title.trim(),
			...(description ? { description } : {}),
			...(draft.cover ? { coverImageId: draft.cover.id } : {}),
			...(draft.prepMinutes !== null ? { prepMinutes: draft.prepMinutes } : {}),
			...(draft.cookMinutes !== null ? { cookMinutes: draft.cookMinutes } : {}),
			ingredients,
			prepSteps: steps('prepSteps'),
			instructionSteps: steps('instructionSteps')
		},
		maps
	};
}

/** The body of `POST /recipes` (add `visibility`) and `PUT /recipes/{id}` (add `version`, `deleteVariants`). */
export function toRecipeWrite(
	draft: RecipeDraft,
	ids: { tagIds: string[]; utensilIds: string[] }
): { write: Schemas['RecipeWrite']; maps: RowMaps } {
	const { body, maps } = buildBody(draft);
	return { write: { ...body, tagIds: ids.tagIds, utensilIds: ids.utensilIds }, maps };
}

/** The `recipe` part of a `VariantWrite` (no title, description, cover or tags). */
export function toVariantRecipeWrite(
	draft: RecipeDraft,
	utensilIds: string[]
): { recipe: Schemas['VariantRecipeWrite']; maps: RowMaps } {
	const { body, maps } = buildBody(draft);
	return {
		recipe: {
			...(body.prepMinutes !== undefined ? { prepMinutes: body.prepMinutes } : {}),
			...(body.cookMinutes !== undefined ? { cookMinutes: body.cookMinutes } : {}),
			utensilIds,
			ingredients: body.ingredients,
			prepSteps: body.prepSteps,
			instructionSteps: body.instructionSteps
		},
		maps
	};
}

/** A stable string for "did the user change anything" (row keys are ignored). */
export function draftSignature(draft: RecipeDraft): string {
	return JSON.stringify(draft, (key, value: unknown) => (key === 'key' ? undefined : value));
}

export type FieldErrors = Record<string, string>;

const fe = t.recipes.fieldErrors;

/** Client validation with the API's limits; paths use editor row indices. */
export function validateDraft(draft: RecipeDraft, mode: EditorMode = 'recipe'): FieldErrors {
	const errors: FieldErrors = {};
	if (mode === 'recipe') {
		if (draft.title.trim() === '') errors.title = fe.required;
		else if (draft.title.trim().length > LIMITS.title) errors.title = fe.too_long;
		if (draft.description.length > LIMITS.description) errors.description = fe.too_long;
	}
	for (const key of ['prepMinutes', 'cookMinutes'] as const) {
		const v = draft[key];
		if (v !== null && (!Number.isInteger(v) || v < 0 || v > LIMITS.minutes)) {
			errors[key] = fe.minutesRange;
		}
	}
	const active = draft.ingredients.filter((i) => !i.removed);
	if (active.length < 1 || active.length > LIMITS.ingredients)
		errors.ingredients = fe.ingredientsRange;
	draft.ingredients.forEach((i, index) => {
		if (i.removed) return;
		const path = `ingredients[${index}]`;
		if (!i.product) errors[`${path}.product`] = fe.productRequired;
		if (i.note.length > LIMITS.note) errors[`${path}.note`] = fe.too_long;
		if (!i.toTaste) {
			if (
				i.value === null ||
				i.value <= 0 ||
				i.unit === null ||
				(i.unit === 'custom' && !i.customUnit)
			) {
				errors[`${path}.quantity`] = fe.quantityRequired;
			} else if (
				i.product &&
				toBaseAmount(
					{ value: i.value, unit: i.unit, customUnitId: i.customUnit?.id ?? null },
					i.product,
					i.customUnit
				) === null
			) {
				errors[`${path}.quantity`] = fe.unitIncompatible;
			}
		}
	});
	const checkSteps = (key: 'prepSteps' | 'instructionSteps', min: number) => {
		const active = draft[key].filter((s) => !s.removed);
		if (active.length < min || active.length > LIMITS.steps) {
			errors[key] = key === 'prepSteps' ? fe.prepRange : fe.instructionsRange;
		}
		draft[key].forEach((s, index) => {
			if (s.removed) return;
			const blocks = toContentBlocks(s.blocks);
			const path = `${key}[${index}]`;
			if (blocks.length === 0) errors[path] = fe.stepEmpty;
			else if (
				blocks.some(
					(b) =>
						(b.type === 'text' && b.text.length > LIMITS.text) ||
						(b.type === 'image' && (b.caption?.length ?? 0) > LIMITS.caption)
				)
			) {
				errors[path] = fe.too_long;
			}
		});
	};
	checkSteps('prepSteps', 0);
	checkSteps('instructionSteps', 1);
	return errors;
}

function messageFor(code: string): string {
	const known = (fe as Record<string, string>)[code];
	if (known) return known;
	return fe.generic;
}

/**
 * Server `errors` (payload paths such as `ingredients[2].quantity`) → editor paths. Step block paths
 * collapse onto their step. Unknown paths are kept as they are.
 */
export function mapServerErrors(
	errors: Record<string, string[]>,
	maps: RowMaps,
	mode: EditorMode = 'recipe'
): FieldErrors {
	const out: FieldErrors = {};
	for (const [path, codes] of Object.entries(errors)) {
		const code = codes[0];
		if (code === undefined) continue;
		const match = /^(ingredients|prepSteps|instructionSteps)\[(\d+)\](.*)$/.exec(path);
		let target = path;
		if (match) {
			const section = match[1] as keyof RowMaps;
			const row = maps[section][Number(match[2])];
			if (row === undefined) continue;
			const rest = match[3] ?? '';
			target = section === 'ingredients' ? `${section}[${row}]${rest}` : `${section}[${row}]`;
		}
		if (mode === 'variant' && (path === 'title' || path === 'description')) continue;
		out[target] ??= messageFor(code);
	}
	return out;
}

/** URL of a stored image. */
export function imageUrl(imageId: string, size: 'full' | 'thumb' = 'full'): string {
	return size === 'thumb' ? `/api/v1/images/${imageId}?size=thumb` : `/api/v1/images/${imageId}`;
}
