// Polish plural categories (PROJECT §5.5).

/** Integers: 1 is `one`; ending in 2–4 but not 12–14 is `few`; the rest is `many`. Any fraction is `fraction`. */
export function pluralCategory(n: number): 'one' | 'few' | 'many' | 'fraction' {
	if (!Number.isInteger(n)) return 'fraction';
	const a = Math.abs(n);
	if (a === 1) return 'one';
	const last = a % 10;
	const lastTwo = a % 100;
	if (last >= 2 && last <= 4 && !(lastTwo >= 12 && lastTwo <= 14)) return 'few';
	return 'many';
}
