// Unit words and their plural forms (PROJECT §5.5). Owned by F1.
// Forms are one / few / many / fraction, e.g. 1 szklanka, 2 szklanki, 5 szklanek, ½ szklanki.
type Forms = {
	readonly one: string;
	readonly few: string;
	readonly many: string;
	readonly fraction: string;
};

export const units = {
	g: 'g',
	kg: 'kg',
	ml: 'ml',
	l: 'l',
	glass: { one: 'szklanka', few: 'szklanki', many: 'szklanek', fraction: 'szklanki' },
	tbsp: { one: 'łyżka', few: 'łyżki', many: 'łyżek', fraction: 'łyżki' },
	tsp: { one: 'łyżeczka', few: 'łyżeczki', many: 'łyżeczek', fraction: 'łyżeczki' },
	drop: { one: 'kropla', few: 'krople', many: 'kropli', fraction: 'kropli' },
	pinch: { one: 'szczypta', few: 'szczypty', many: 'szczypt', fraction: 'szczypty' },
	/** Piece sizes; the fraction form is the genitive used in "¼ średniej". */
	sizes: {
		small: { one: 'mała', few: 'małe', many: 'małych', fraction: 'małej' },
		medium: { one: 'średnia', few: 'średnie', many: 'średnich', fraction: 'średniej' },
		large: { one: 'duża', few: 'duże', many: 'dużych', fraction: 'dużej' }
	},
	/** Default piece name for products without their own forms. */
	piece: { one: 'szt.', few: 'szt.', many: 'szt.', fraction: 'szt.' }
} as const satisfies Record<string, string | Forms | Record<string, Forms>>;
