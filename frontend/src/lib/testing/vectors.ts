// Loader for shared/test-vectors (TECHNICAL §6). Test-only: it uses node:fs and is never imported by app code.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type {
	DisplayClass,
	LiquidUnits,
	MeasureKind,
	PluralForms,
	Quantity,
	VariantMatrix
} from '$lib/domain/types';

export type VectorKind = 'quantities' | 'formatting' | 'variants' | 'matching';

/** Every case has a unique id prefixed with the task id, e.g. `F1-onion-2.8`. */
export type VectorCase = { id: string; description?: string };

/** `quantities/`: quantity + product → base amount (null ⇒ incompatible). */
export type QuantityVector = VectorCase & {
	quantity: Quantity;
	product: {
		measureKind: MeasureKind;
		hasSizes: boolean;
		gramsPerMl: number | null;
		gramsPerPiece: number | null;
	};
	expected: number | null;
};

/** `formatting/`: base amount + product + options → Polish text (without `~`) and the approximate flag. */
export type FormattingVector = VectorCase & {
	base: number;
	product: {
		measureKind: MeasureKind;
		displayClass: DisplayClass;
		hasSizes: boolean;
		allowHalf: boolean;
		pieceForms: PluralForms | null;
	};
	options: { liquidUnits: LiquidUnits; context: 'recipe' | 'inventory' };
	expected: { text: string; approximate: boolean };
};

/** `variants/`: base + variant ops → resolved recipes and the matrix (see the schema for the full shape). */
export type VariantVector = VectorCase & {
	base: unknown;
	variants: unknown[];
	expectedResolved: Record<string, unknown>;
	expectedMatrix: VariantMatrix;
};

/** `matching/`: matrix + picks + preferences → match result essentials (shape owned by F2). */
export type MatchingVector = VectorCase & Record<string, unknown>;

export const VECTORS_ROOT = fileURLToPath(
	new URL('../../../../shared/test-vectors/', import.meta.url)
);

/** Reads every `*.json` file of `dir` (sorted by name) and returns all cases; fails on duplicate ids. */
export function loadVectorsFrom<T extends VectorCase>(dir: string): T[] {
	const seen = new Map<string, string>();
	const cases: T[] = [];
	const files = readdirSync(dir)
		.filter((name) => name.endsWith('.json'))
		.sort();

	for (const file of files) {
		const parsed: unknown = JSON.parse(readFileSync(join(dir, file), 'utf8'));
		if (
			typeof parsed !== 'object' ||
			parsed === null ||
			!Array.isArray((parsed as { cases?: unknown }).cases)
		) {
			throw new Error(`${file}: expected an object with a "cases" array`);
		}
		for (const item of (parsed as { cases: unknown[] }).cases) {
			const id = (item as { id?: unknown }).id;
			if (typeof id !== 'string' || id.length === 0) throw new Error(`${file}: a case has no "id"`);
			const previous = seen.get(id);
			if (previous) throw new Error(`Duplicate test-vector id "${id}" in ${previous} and ${file}`);
			seen.set(id, file);
			cases.push(item as T);
		}
	}
	return cases;
}

/** All cases of one kind from `shared/test-vectors/<kind>/`. */
export function loadVectors<T extends VectorCase>(kind: VectorKind): T[] {
	return loadVectorsFrom<T>(join(VECTORS_ROOT, kind));
}
