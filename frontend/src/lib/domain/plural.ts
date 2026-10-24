// Polish plural categories (PROJECT §5.5).
import { pluralSuffixRules } from '$lib/i18n/pl/units';
import type { PluralForms } from './types';

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

/**
 * A first guess at the four forms of a new custom unit's name (PROJECT §5.6), e.g. "puszka" →
 * puszka / puszki / puszek / puszki. Only the first word is inflected; the user corrects the rest.
 */
export function suggestPluralForms(name: string): PluralForms {
	const clean = name.trim().replace(/\s+/g, ' ');
	const space = clean.indexOf(' ');
	const head = space < 0 ? clean : clean.slice(0, space);
	const tail = space < 0 ? '' : clean.slice(space);
	const lower = head.toLowerCase();
	const rule = pluralSuffixRules.find((r) => lower.endsWith(r.suffix) && lower.length > r.suffix.length);
	if (!rule) return { one: clean, few: clean, many: clean, fraction: clean };
	const stem = head.slice(0, head.length - rule.suffix.length);
	return {
		one: clean,
		few: stem + rule.few + tail,
		many: stem + rule.many + tail,
		fraction: stem + rule.fraction + tail
	};
}
