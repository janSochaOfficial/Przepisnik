// Tags and utensils are typed as names in the editor; the API wants ids (API §2). New names are
// created on save, and the server answers with the existing entry for a normalized match.
import { createApi, unwrap } from '$lib/api/client';

export type NameKind = 'tags' | 'utensils';
export type Named = { id: string; name: string };

const same = (a: string, b: string) =>
	a.trim().toLocaleLowerCase('pl') === b.trim().toLocaleLowerCase('pl');

/** Up to 50 known names for the autocomplete; a failure just means no suggestions. */
export async function loadNames(kind: NameKind, q?: string): Promise<Named[]> {
	const api = createApi({ onUnauthorized: null });
	try {
		const query = { limit: 50, ...(q ? { q } : {}) };
		const items = await unwrap(
			kind === 'tags'
				? api.GET('/tags', { params: { query } })
				: api.GET('/utensils', { params: { query } })
		);
		return items.filter((i) => !('isSystem' in i && i.isSystem));
	} catch {
		return [];
	}
}

/** Ids for `names`: known ones are reused, the others are created (`POST /tags`, `POST /utensils`). */
export async function resolveNames(
	kind: NameKind,
	names: string[],
	known: readonly Named[] = []
): Promise<string[]> {
	const api = createApi();
	const ids: string[] = [];
	for (const name of names) {
		const hit = known.find((k) => same(k.name, name));
		if (hit) {
			ids.push(hit.id);
			continue;
		}
		const created = await unwrap(
			kind === 'tags'
				? api.POST('/tags', { body: { name } })
				: api.POST('/utensils', { body: { name } })
		);
		ids.push(created.id);
	}
	return [...new Set(ids)];
}
