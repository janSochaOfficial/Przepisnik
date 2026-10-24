// A small cached client for the custom-unit catalog (API.md §2.1). Lists are cached per product for
// the page's lifetime; a unit created here is added to every cached list it belongs to.
import { createApi, unwrap } from '$lib/api/client';
import type { CustomUnitRef, CustomUnitWrite } from '$lib/domain/types';

const cache = new Map<string, Promise<CustomUnitRef[]>>();

const keyOf = (productId: string | null) => productId ?? '';

/** The units for every product plus the units of `productId` (product units first). */
export function loadUnits(productId: string | null): Promise<CustomUnitRef[]> {
	const key = keyOf(productId);
	let entry = cache.get(key);
	if (!entry) {
		const query = productId ? { productId, limit: 50 } : { limit: 50 };
		entry = unwrap(createApi().GET('/units', { params: { query } }));
		// A failed request is not cached, so the next picker tries again.
		entry.catch(() => cache.delete(key));
		cache.set(key, entry);
	}
	return entry;
}

/** Adds `unit` to the cached lists it belongs to (its product's, or every list for a global unit). */
export function rememberUnit(unit: CustomUnitRef): void {
	for (const [key, entry] of cache) {
		if (unit.productId !== null && key !== unit.productId) continue;
		cache.set(
			key,
			entry.then((list) => (list.some((u) => u.id === unit.id) ? list : [...list, unit]))
		);
	}
}

/** Creates a unit, or gets the existing one with the same name in the same scope. */
export async function createUnit(body: CustomUnitWrite): Promise<CustomUnitRef> {
	const unit = await unwrap(createApi().POST('/units', { body }));
	rememberUnit(unit);
	return unit;
}

/** Test hook: forgets every cached list. */
export function clearUnitCache(): void {
	cache.clear();
}
