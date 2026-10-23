// Parsing and printing of decimal numbers typed by a Polish user (decimal comma, PROJECT §1).

/** The result of reading a number field: empty, a number, or text that is not a number. */
export type DecimalParse =
	{ kind: 'empty' } | { kind: 'number'; value: number } | { kind: 'invalid' };

// Digits with at most one decimal separator; a leading minus is allowed. Spaces (also the narrow
// no-break space that pl-PL uses for grouping; `\s` covers it) are ignored, so "1 250,5" works.
const DECIMAL = /^-?(\d+([.,]\d*)?|[.,]\d+)$/;

/**
 * Reads `"1,5"`, `"1.5"`, `",5"`, `"1 250"` and `"2,"`. Rejects anything else (`"abc"`, `"1,2,3"`).
 * The comma is the Polish decimal separator; the dot is accepted because phone keyboards differ.
 */
export function parseDecimal(text: string): DecimalParse {
	const compact = text.replace(/\s/g, '');
	if (compact === '') return { kind: 'empty' };
	if (!DECIMAL.test(compact)) return { kind: 'invalid' };
	const value = Number(compact.replace(',', '.'));
	return Number.isFinite(value) ? { kind: 'number', value } : { kind: 'invalid' };
}

const formatter = new Intl.NumberFormat('pl-PL', { useGrouping: false, maximumFractionDigits: 6 });

/** Prints a number for an input field: decimal comma, no grouping (`1.5` → `"1,5"`). */
export function formatDecimal(value: number | null | undefined): string {
	return value === null || value === undefined || !Number.isFinite(value)
		? ''
		: formatter.format(value);
}
