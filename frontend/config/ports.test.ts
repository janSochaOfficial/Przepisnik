import { mkdtempSync, mkdirSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { describe, expect, it } from 'vitest';
import { loadPlaywrightWorkers } from './ports.ts';

/** A fake worktree: a `.git` directory and, optionally, a `.env.local` with `content`. */
function worktree(content?: string): string {
	const root = mkdtempSync(join(tmpdir(), 'ports-'));
	mkdirSync(join(root, '.git'));
	if (content !== undefined) writeFileSync(join(root, '.env.local'), content);
	return root;
}

describe('loadPlaywrightWorkers', () => {
	it('keeps the Playwright default when nothing is set', () => {
		expect(loadPlaywrightWorkers({}, worktree())).toBeUndefined();
	});

	it('reads PLAYWRIGHT_WORKERS from .env.local', () => {
		expect(loadPlaywrightWorkers({}, worktree('API_PORT=5101\nPLAYWRIGHT_WORKERS=2\n'))).toBe(2);
	});

	it('prefers the exported variable over .env.local', () => {
		const root = worktree('PLAYWRIGHT_WORKERS=2');
		expect(loadPlaywrightWorkers({ PLAYWRIGHT_WORKERS: '4' }, root)).toBe(4);
	});

	it.each(['0', '-1', '1.5', 'two', ''])('ignores the invalid value %j', (value) => {
		expect(loadPlaywrightWorkers({}, worktree(`PLAYWRIGHT_WORKERS=${value}`))).toBeUndefined();
	});
});
