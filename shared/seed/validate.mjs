// Validates shared/seed/*.json against the rules in the matching *.schema.json.
// Run: node shared/seed/validate.mjs   (no dependencies; a tiny JSON Schema subset validator).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = path.dirname(fileURLToPath(import.meta.url));
const errors = [];
const err = (m) => errors.push(m);
const read = (f) => JSON.parse(fs.readFileSync(path.join(dir, f), 'utf8'));

// Mirrors backend NameNormalizer: NFD, drop combining marks, collapse whitespace, lower-case, ł/ø/đ folded.
export function normalize(s) {
  return s
    .normalize('NFD')
    .replace(/\p{Mn}/gu, '')
    .toLowerCase()
    .replace(/[łøđ]/g, (c) => ({ ł: 'l', ø: 'o', đ: 'd' })[c])
    .split(/\s+/)
    .filter(Boolean)
    .join(' ')
    .normalize('NFC');
}

const typeOf = (v) => (v === null ? 'null' : Array.isArray(v) ? 'array' : Number.isInteger(v) ? 'integer' : typeof v);
const isType = (v, t) => (t === 'number' ? typeof v === 'number' : t === 'integer' ? Number.isInteger(v) : typeOf(v) === t);

// Subset: type, enum, const, required, properties, additionalProperties:false, items, minLength, maxLength,
// exclusiveMinimum, oneOf, allOf, if/then, contains/minContains/maxContains.
function check(v, s, at) {
  const n = (m) => err(`${at}: ${m}`);
  if (s.type) {
    const ts = [].concat(s.type);
    if (!ts.some((t) => isType(v, t))) return n(`expected ${ts.join('|')}, got ${typeOf(v)}`);
  }
  if ('const' in s && v !== s.const) n(`expected const ${JSON.stringify(s.const)}`);
  if (s.enum && !s.enum.includes(v)) n(`${JSON.stringify(v)} not in enum`);
  if (typeof v === 'string') {
    if (s.minLength != null && [...v].length < s.minLength) n('too short');
    if (s.maxLength != null && [...v].length > s.maxLength) n('too long');
  }
  if (typeof v === 'number' && s.exclusiveMinimum != null && !(v > s.exclusiveMinimum)) n(`must be > ${s.exclusiveMinimum}`);
  if (s.oneOf) {
    const ok = s.oneOf.filter((sub) => validCount(v, sub) === 0).length;
    if (ok !== 1) n(`matches ${ok} oneOf branches`);
  }
  for (const sub of s.allOf ?? []) {
    if (sub.if) {
      if (validCount(v, sub.if) === 0 && sub.then) check(v, sub.then, at);
    } else check(v, sub, at);
  }
  if (v && typeof v === 'object' && !Array.isArray(v)) {
    for (const r of s.required ?? []) if (!(r in v)) n(`missing "${r}"`);
    for (const [k, sub] of Object.entries(s.properties ?? {})) if (k in v) check(v[k], sub, `${at}.${k}`);
    if (s.additionalProperties === false) {
      for (const k of Object.keys(v)) if (!(k in (s.properties ?? {})) && k !== '$schema') n(`unexpected "${k}"`);
    }
  }
  if (Array.isArray(v)) {
    if (s.items) v.forEach((x, i) => check(x, s.items, `${at}[${i}]`));
    if (s.contains) {
      const c = v.filter((x) => validCount(x, s.contains) === 0).length;
      if (c < (s.minContains ?? 1)) n(`needs at least ${s.minContains ?? 1} matching "contains" item(s), found ${c}`);
      if (s.maxContains != null && c > s.maxContains) n(`at most ${s.maxContains} matching "contains" item(s), found ${c}`);
    }
  }
}
function validCount(v, s) {
  const saved = errors.length;
  check(v, s, '?');
  return errors.splice(saved).length;
}

function validateFile(file, key) {
  const schema = read(`${file}.schema.json`);
  const data = read(`${file}.json`);
  check(data, schema, file);
  const names = new Map();
  for (const [i, item] of (data[key] ?? []).entries()) {
    if (typeof item.name !== 'string') continue;
    if (item.name !== item.name.trim() || /\s{2,}/.test(item.name)) err(`${file}[${i}]: untidy whitespace in "${item.name}"`);
    const norm = normalize(item.name);
    if (names.has(norm)) err(`${file}[${i}]: "${item.name}" duplicates "${names.get(norm)}" after normalization`);
    names.set(norm, item.name);
  }
  return data[key] ?? [];
}

const products = validateFile('products', 'products');
const tags = validateFile('tags', 'tags');
const utensils = validateFile('utensils', 'utensils');

// Rules beyond the schema (task S1 acceptance and PROJECT §5, §7.2).
const kindOf = { weight: 'mass', liquid: 'volume', spice: 'volume', piece: 'count' };
const categories = new Set(read('products.schema.json').properties.products.items.properties.category.enum);
for (const p of products) {
  if (kindOf[p.displayClass] !== p.measureKind) err(`product "${p.name}": displayClass ${p.displayClass} does not match measureKind ${p.measureKind}`);
  if (p.measureKind === 'count' && !p.gramsPerPiece) err(`product "${p.name}": count product without gramsPerPiece`);
  if (p.measureKind !== 'count' && p.gramsPerPiece != null) err(`product "${p.name}": gramsPerPiece on a non-count product`);
  if (p.measureKind === 'count' && p.gramsPerMl != null) err(`product "${p.name}": gramsPerMl on a count product`);
  if (p.pieceForms && p.measureKind !== 'count') err(`product "${p.name}": pieceForms on a non-count product`);
  if (/[A-ZĄĆĘŁŃÓŚŹŻ]/.test(p.name[0])) err(`product "${p.name}": must start lower-case`);
}
const present = new Set(products.map((p) => p.category));
for (const c of categories) if (!present.has(c)) err(`no product in category "${c}"`);
if (products.length < 150) err(`need >= 150 products, have ${products.length}`);
if (tags.length < 25) err(`need >= 25 tags, have ${tags.length}`);
if (utensils.length < 30) err(`need >= 30 utensils, have ${utensils.length}`);
for (const t of tags) if (t.isSystem && t.name !== 'szybkie') err(`tag "${t.name}": only "szybkie" may be a system tag`);

if (errors.length) {
  console.error(errors.join('\n'));
  console.error(`\nFAILED: ${errors.length} problem(s)`);
  process.exit(1);
}
console.log(`OK: ${products.length} products, ${tags.length} tags, ${utensils.length} utensils`);
