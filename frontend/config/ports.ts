import { existsSync, readFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

/**
 * Ports for this worktree (IMPLEMENTATION_PLAN §4.3). Exported variables win; otherwise the git-ignored
 * `.env.local` at the worktree root is read; otherwise the standard defaults (TECHNICAL §8) apply.
 */
export type Ports = { apiPort: number; webPort: number; previewPort: number };

const DEFAULTS = { API_PORT: 5080, WEB_PORT: 5173, PREVIEW_PORT: 4173 } as const;

/** Walks up from `start` to the repository root (the first directory with `.git`) looking for `.env.local`. */
export function findEnvLocal(start: string): string | null {
	let dir = resolve(start);
	for (;;) {
		const candidate = join(dir, '.env.local');
		if (existsSync(candidate)) return candidate;
		if (existsSync(join(dir, '.git'))) return null;
		const parent = dirname(dir);
		if (parent === dir) return null;
		dir = parent;
	}
}

/** Parses `KEY=value` lines; `#` comments, blank lines and an `export ` prefix are allowed. */
export function parseEnv(text: string): Record<string, string> {
	const out: Record<string, string> = {};
	for (const raw of text.split(/\r?\n/)) {
		const line = raw.trim().replace(/^export\s+/, '');
		if (!line || line.startsWith('#')) continue;
		const eq = line.indexOf('=');
		if (eq <= 0) continue;
		out[line.slice(0, eq).trim()] = line
			.slice(eq + 1)
			.trim()
			.replace(/^['"]|['"]$/g, '');
	}
	return out;
}

/** Reads `KEY` from the exported variables, then from `.env.local` (same precedence as the ports). */
function readSetting(key: string, env: NodeJS.ProcessEnv, start: string): string | undefined {
	if (env[key]) return env[key];
	const file = findEnvLocal(start);
	return file ? parseEnv(readFileSync(file, 'utf8'))[key] : undefined;
}

function toPort(value: string | undefined, fallback: number): number {
	const n = Number(value);
	return Number.isInteger(n) && n > 0 && n < 65536 ? n : fallback;
}

export function loadPorts(
	env: NodeJS.ProcessEnv = process.env,
	start: string = dirname(fileURLToPath(import.meta.url))
): Ports {
	const file = findEnvLocal(start);
	const local = file ? parseEnv(readFileSync(file, 'utf8')) : {};
	const pick = (key: keyof typeof DEFAULTS) => toPort(env[key] || local[key], DEFAULTS[key]);
	return {
		apiPort: pick('API_PORT'),
		webPort: pick('WEB_PORT'),
		previewPort: pick('PREVIEW_PORT')
	};
}

/**
 * Playwright worker count from `PLAYWRIGHT_WORKERS` (COORDINATOR §2: 2 while two task agents run).
 * `undefined` (unset or not a positive integer) keeps Playwright's default.
 */
export function loadPlaywrightWorkers(
	env: NodeJS.ProcessEnv = process.env,
	start: string = dirname(fileURLToPath(import.meta.url))
): number | undefined {
	const n = Number(readSetting('PLAYWRIGHT_WORKERS', env, start));
	return Number.isInteger(n) && n > 0 ? n : undefined;
}
