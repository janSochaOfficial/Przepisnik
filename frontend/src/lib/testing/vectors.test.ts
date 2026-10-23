// @vitest-environment node
import { mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { afterEach, describe, expect, it } from 'vitest';
import {
	loadVectors,
	loadVectorsFrom,
	type FormattingVector,
	type QuantityVector,
	type VariantVector
} from './vectors';

describe('loadVectorsFrom', () => {
	let dir: string | undefined;
	afterEach(() => {
		if (dir) rmSync(dir, { recursive: true, force: true });
		dir = undefined;
	});

	function writeVectors(files: Record<string, unknown>): string {
		dir = mkdtempSync(join(tmpdir(), 'vectors-'));
		for (const [name, content] of Object.entries(files)) {
			writeFileSync(join(dir, name), JSON.stringify(content));
		}
		return dir;
	}

	it('reads every json file of the directory', () => {
		const path = writeVectors({
			'A.json': { cases: [{ id: 'A-1' }] },
			'B.json': { cases: [{ id: 'B-1' }, { id: 'B-2' }] },
			'notes.txt': 'ignored'
		});
		expect(loadVectorsFrom(path).map((c) => c.id)).toEqual(['A-1', 'B-1', 'B-2']);
	});

	it('fails on a duplicate id across files', () => {
		const path = writeVectors({
			'A.json': { cases: [{ id: 'X-1' }] },
			'B.json': { cases: [{ id: 'X-1' }] }
		});
		expect(() => loadVectorsFrom(path)).toThrow(
			/Duplicate test-vector id "X-1" in A.json and B.json/
		);
	});

	it('fails on a file without a cases array', () => {
		const path = writeVectors({ 'A.json': [{ id: 'A-1' }] });
		expect(() => loadVectorsFrom(path)).toThrow(/"cases" array/);
	});
});

describe('shared test vectors', () => {
	it('load for every kind without duplicate ids', () => {
		expect(loadVectors<QuantityVector>('quantities').length).toBeGreaterThanOrEqual(25);
		expect(loadVectors<FormattingVector>('formatting').length).toBeGreaterThanOrEqual(30);
		expect(loadVectors<VariantVector>('variants').length).toBeGreaterThanOrEqual(5);
		expect(loadVectors('matching')).toBeInstanceOf(Array);
	});
});
