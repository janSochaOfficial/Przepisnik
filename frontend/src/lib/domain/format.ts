// Display formatting of amounts (PROJECT §5.4); checked against shared/test-vectors/formatting/.
import { units } from '$lib/i18n/pl/units';
import { bestPieceCombination } from './pieces';
import { pluralCategory } from './plural';
import type {
	Ingredient,
	LiquidUnits,
	PieceSize,
	PluralForms,
	ProductRef,
	Quantity
} from './types';
import { ML_PER, SIZE_FACTOR } from './units';

/** `inventory` ⇒ PROJECT §5.4 "Inventory format" (always metric for liquids and spices). */
export type FormatOptions = { liquidUnits: LiquidUnits; context?: 'recipe' | 'inventory' };

/** `text` never contains the `~` marker; `approximate` says whether to show it (> 5 % off). */
export type FormattedAmount = { text: string; approximate: boolean };

const EPS = 1e-9;
const APPROXIMATE_ABOVE = 0.05;
const QUARTER_TSP_ML = ML_PER.tsp / 4;
const QUARTER_GLASS_ML = ML_PER.glass / 4;
const GLYPHS: Record<number, string> = { 0.25: '¼', 0.5: '½', 0.75: '¾' };

/** Decimal-comma number (`1,25`), never grouped. */
export function formatNumberPl(n: number, maxFractionDigits = 2): string {
	const text = new Intl.NumberFormat('pl-PL', {
		maximumFractionDigits: maxFractionDigits,
		useGrouping: false
	}).format(n);
	// Intl renders a negative zero as "-0".
	return text === '-0' ? '0' : text;
}

/** Whole numbers and multiples of ¼ use the glyphs (`1 ½`, `¾`); anything else a decimal comma. */
function formatQuantityNumber(n: number): string {
	const whole = Math.floor(n + EPS);
	const frac = Math.round((n - whole) * 4) / 4;
	if (Math.abs(n - whole - frac) < EPS) {
		if (frac === 0) return formatNumberPl(whole, 0);
		const glyph = GLYPHS[frac];
		if (glyph) return whole === 0 ? glyph : `${whole} ${glyph}`;
	}
	return formatNumberPl(n);
}

function word(forms: PluralForms, n: number): string {
	return forms[pluralCategory(n)];
}

/** Number plus the matching plural form, e.g. "2 tbsp-few", "1 ½ glass-fraction". */
function withForms(n: number, forms: PluralForms): string {
	return `${formatQuantityNumber(n)} ${word(forms, n)}`;
}

/** Nearest multiple of `step`, ties upwards, tolerant of floating-point noise. */
function roundTo(v: number, step: number): number {
	return Math.round(v / step + EPS) * step;
}

type Rendered = { text: string; shown: number };

function weight(g: number): Rendered {
	if (g <= 0) return { text: `0 ${units.g}`, shown: 0 };
	let shown: number;
	if (g < 10) shown = Math.max(1, roundTo(g, 1));
	else if (g < 100) shown = roundTo(g, 5);
	else if (g < 1000) shown = roundTo(g, 10);
	else shown = roundTo(g, 50);
	if (shown >= 1000) return { text: `${formatNumberPl(shown / 1000)} ${units.kg}`, shown };
	return { text: `${formatNumberPl(shown, 0)} ${units.g}`, shown };
}

function litres(ml: number): Rendered {
	const shown = roundTo(ml, 100);
	return { text: `${formatNumberPl(shown / 1000, 1)} ${units.l}`, shown };
}

function millilitres(ml: number, step: number): Rendered {
	const shown = Math.max(1, roundTo(ml, step));
	return { text: `${formatNumberPl(shown, 0)} ${units.ml}`, shown };
}

/** Rounds to ¼ teaspoon, then splits greedily into whole tablespoons and teaspoons. */
function spoons(ml: number): Rendered {
	const quarters = Math.round(ml / QUARTER_TSP_ML + EPS);
	const quartersPerTbsp = ML_PER.tbsp / QUARTER_TSP_ML;
	const tbsp = Math.floor(quarters / quartersPerTbsp);
	const tspQuarters = quarters - tbsp * quartersPerTbsp;
	const parts: string[] = [];
	if (tbsp > 0) parts.push(withForms(tbsp, units.tbsp));
	if (tspQuarters > 0) parts.push(withForms(tspQuarters / 4, units.tsp));
	return { text: parts.join(' + '), shown: quarters * QUARTER_TSP_ML };
}

function drops(ml: number): Rendered {
	const n = Math.max(1, Math.round(ml / ML_PER.drop + EPS));
	return { text: withForms(n, units.drop), shown: n * ML_PER.drop };
}

function pinches(ml: number): Rendered {
	const n = Math.max(1, Math.round(ml / ML_PER.pinch + EPS));
	return { text: withForms(n, units.pinch), shown: n * ML_PER.pinch };
}

function metricVolume(ml: number): Rendered {
	if (ml > 1000) return litres(ml);
	if (ml < 10) return millilitres(ml, 1);
	if (ml < 100) return millilitres(ml, 5);
	return millilitres(ml, 10);
}

function liquid(ml: number, o: FormatOptions): Rendered {
	if (o.context === 'inventory') return metricVolume(ml);
	if (ml > 1000) return litres(ml);
	if (ml >= 62.5 - EPS) {
		if (o.liquidUnits === 'metric') return millilitres(ml, 10);
		const quarters = Math.round(ml / QUARTER_GLASS_ML + EPS);
		return { text: withForms(quarters / 4, units.glass), shown: quarters * QUARTER_GLASS_ML };
	}
	if (ml >= 0.625 - EPS) return spoons(ml);
	return drops(ml);
}

function spice(ml: number, o: FormatOptions): Rendered {
	if (o.context === 'inventory') return metricVolume(ml);
	if (ml >= 0.625 - EPS) return spoons(ml);
	return pinches(ml);
}

function pieceForms(p: ProductRef): PluralForms {
	return p.pieceForms ?? units.piece;
}

function wholePieces(t: number, p: ProductRef): Rendered {
	const shown = p.allowHalf ? Math.max(0.5, roundTo(t, 0.5)) : Math.max(1, Math.round(t + EPS));
	return { text: withForms(shown, pieceForms(p)), shown };
}

function sizedPieces(t: number): Rendered {
	const combo = bestPieceCombination(t);
	if ('fractionOfMedium' in combo) {
		return {
			text: `${GLYPHS[combo.fractionOfMedium]} ${units.sizes.medium.fraction}`,
			shown: combo.fractionOfMedium
		};
	}
	const sizes: [PieceSize, number][] = [
		['large', combo.large],
		['medium', combo.medium],
		['small', combo.small]
	];
	const parts = sizes.filter(([, n]) => n > 0).map(([s, n]) => `${n} ${word(units.sizes[s], n)}`);
	const shown = sizes.reduce((sum, [s, n]) => sum + n * SIZE_FACTOR[s], 0);
	return { text: parts.join(' + '), shown };
}

function piece(t: number, p: ProductRef, o: FormatOptions): Rendered {
	if (o.context !== 'inventory' && p.hasSizes) return sizedPieces(t);
	return wholePieces(t, p);
}

function zero(p: ProductRef): FormattedAmount {
	const u =
		p.displayClass === 'weight'
			? units.g
			: p.displayClass === 'piece'
				? units.piece.many
				: units.ml;
	return { text: `0 ${u}`, approximate: false };
}

/** Formats a base amount (g, ml or medium pieces) with the display rules of PROJECT §5.4. */
export function formatBaseAmount(base: number, p: ProductRef, o: FormatOptions): FormattedAmount {
	if (!(base > 0)) return zero(p);
	let r: Rendered;
	switch (p.displayClass) {
		case 'weight':
			r = weight(base);
			break;
		case 'liquid':
			r = liquid(base, o);
			break;
		case 'spice':
			r = spice(base, o);
			break;
		default:
			r = piece(base, p, o);
	}
	return { text: r.text, approximate: Math.abs(r.shown - base) / base > APPROXIMATE_ABOVE + EPS };
}

/** The quantity exactly as the author entered it, with plural-inflected unit words. */
export function formatQuantityAsEntered(q: Quantity, p: ProductRef): string {
	switch (q.unit) {
		case 'g':
		case 'kg':
		case 'ml':
		case 'l':
			return `${formatQuantityNumber(q.value)} ${units[q.unit]}`;
		case 'piece': {
			const forms = p.hasSizes && q.size ? units.sizes[q.size] : pieceForms(p);
			return withForms(q.value, forms);
		}
		default:
			return withForms(q.value, units[q.unit]);
	}
}

/** Factor 1 ⇒ as entered; otherwise the scaled base amount. Null ⇒ "do smaku". */
export function formatIngredientAmount(
	i: Ingredient,
	factor: number,
	o: FormatOptions
): FormattedAmount | null {
	if (i.quantity === null) return null;
	if (factor === 1)
		return { text: formatQuantityAsEntered(i.quantity, i.product), approximate: false };
	if (i.baseAmount === null) {
		// No canonical amount (unit not convertible): scale the entered value itself.
		const scaled = { ...i.quantity, value: i.quantity.value * factor };
		return { text: formatQuantityAsEntered(scaled, i.product), approximate: false };
	}
	return formatBaseAmount(i.baseAmount * factor, i.product, o);
}
