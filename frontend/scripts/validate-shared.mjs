// Validates shared/ JSON (scripts/verify.sh step "shared JSON"):
// - every shared/test-vectors/<kind>/*.json against schemas/<kind>.schema.json, with unique case ids per kind;
// - every shared/seed/<name>.json against <name>.schema.json, with unique normalized names.
import { readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { Ajv2020 } from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';

const shared = fileURLToPath(new URL('../../shared/', import.meta.url));
const vectorsDir = join(shared, 'test-vectors');
const schemasDir = join(vectorsDir, 'schemas');
const seedDir = join(shared, 'seed');

const readJson = (path) => JSON.parse(readFileSync(path, 'utf8'));
const ajv = new Ajv2020({
	allErrors: true,
	strict: true,
	strictRequired: false,
	allowUnionTypes: true
});
addFormats.default(ajv);
ajv.addSchema(readJson(join(schemasDir, 'common.schema.json')), 'common.schema.json');

const failures = [];
const fail = (message) => failures.push(message);
const describeErrors = (errors) =>
	(errors ?? []).map((e) => `  ${e.instancePath || '/'} ${e.message}`).join('\n');

/** Mirrors the backend NameNormalizer: trim, collapse spaces, lower-case, strip diacritics (ł → l). */
export function normalizeName(s) {
	return s
		.normalize('NFD')
		.replace(/\p{Mn}/gu, '')
		.toLowerCase()
		.replace(/ł/g, 'l')
		.replace(/ø/g, 'o')
		.replace(/đ/g, 'd')
		.trim()
		.replace(/\s+/g, ' ');
}

let vectorCount = 0;
for (const kind of ['quantities', 'formatting', 'variants', 'matching']) {
	const validate = ajv.compile(readJson(join(schemasDir, `${kind}.schema.json`)));
	const seen = new Map();
	const files = readdirSync(join(vectorsDir, kind))
		.filter((f) => f.endsWith('.json'))
		.sort();
	for (const file of files) {
		const data = readJson(join(vectorsDir, kind, file));
		if (!validate(data)) fail(`test-vectors/${kind}/${file}:\n${describeErrors(validate.errors)}`);
		for (const c of data.cases ?? []) {
			vectorCount++;
			if (seen.has(c.id))
				fail(`test-vectors/${kind}: duplicate id "${c.id}" in ${seen.get(c.id)} and ${file}`);
			seen.set(c.id, file);
		}
	}
}

const seeds = { products: 'products', tags: 'tags', utensils: 'utensils' };
for (const [name, listKey] of Object.entries(seeds)) {
	const validate = ajv.compile(readJson(join(seedDir, `${name}.schema.json`)));
	const data = readJson(join(seedDir, `${name}.json`));
	if (!validate(data)) fail(`seed/${name}.json:\n${describeErrors(validate.errors)}`);
	const seen = new Set();
	for (const item of data[listKey] ?? []) {
		const normalized = normalizeName(item.name);
		if (seen.has(normalized)) fail(`seed/${name}.json: duplicate normalized name "${normalized}"`);
		seen.add(normalized);
	}
}

if (failures.length > 0) {
	console.error(failures.join('\n'));
	console.error(`\nshared JSON: ${failures.length} problem(s)`);
	process.exit(1);
}
console.log(`shared JSON: OK (${vectorCount} test-vector cases, 3 seed files)`);
