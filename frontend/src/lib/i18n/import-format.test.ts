// @vitest-environment node
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { Ajv2020 } from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';
import { parse } from 'yaml';
import { describe, expect, it } from 'vitest';
import { importFormatExample } from './pl/import-format';

// The help page's example must stay a valid import file: it is checked against the RecipeExport
// schema of the contract (docs/api/openapi.yaml), so the page cannot drift from the format.
const contract = parse(
	readFileSync(fileURLToPath(new URL('../../../../docs/api/openapi.yaml', import.meta.url)), 'utf8')
);

const ajv = new Ajv2020({ allErrors: true, strict: false });
addFormats.default(ajv);
ajv.addSchema(contract, 'openapi');
const validate = ajv.getSchema('openapi#/components/schemas/RecipeExport');

function errorsOf(value: unknown): string {
	if (!validate) throw new Error('RecipeExport schema not found');
	validate(value);
	return (validate.errors ?? []).map((e) => `${e.instancePath || '/'} ${e.message}`).join('; ');
}

describe('import format help example', () => {
	it('is a valid RecipeExport file', () => {
		expect(errorsOf(importFormatExample)).toBe('');
	});

	it('has the shape the page promises: one recipe, two ingredients, one step, one variant, no photos', () => {
		const example = importFormatExample;
		expect(example.recipe.ingredients).toHaveLength(2);
		expect(example.recipe.instructionSteps).toHaveLength(1);
		expect(example.variants).toHaveLength(1);
		expect(example.images).toEqual({});
	});

	it('refers only to products that are declared in the file', () => {
		const keys = new Set(importFormatExample.products.map((p) => p.key));
		for (const i of importFormatExample.recipe.ingredients)
			expect(keys.has(i.productKey)).toBe(true);
	});

	it('fails when a required field is missing, so the check is not vacuous', () => {
		const broken: Record<string, unknown> = { ...importFormatExample };
		delete broken.recipe;
		expect(errorsOf(broken)).not.toBe('');
	});
});
