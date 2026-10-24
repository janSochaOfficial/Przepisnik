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

/**
 * Suffix rules that guess the plural forms of a new custom unit's name (the user corrects them).
 * The first rule whose `suffix` ends the first word wins; the suffix is replaced by each form's ending.
 */
export const pluralSuffixRules: readonly {
	suffix: string;
	few: string;
	many: string;
	fraction: string;
}[] = [
	{ suffix: 'ka', few: 'ki', many: 'ek', fraction: 'ki' }, // puszka, kostka, filiżanka
	{ suffix: 'ga', few: 'gi', many: 'g', fraction: 'gi' },
	{ suffix: 'ja', few: 'je', many: 'ji', fraction: 'ji' }, // porcja
	{ suffix: 'a', few: 'y', many: '', fraction: 'y' }, // szczypta
	{ suffix: 'ek', few: 'ki', many: 'ków', fraction: 'ka' }, // pęczek, kubek
	{ suffix: 'ść', few: 'ści', many: 'ści', fraction: 'ści' }, // garść
	{ suffix: 'ie', few: 'ia', many: 'ń', fraction: 'ia' }, // opakowanie
	{ suffix: 'k', few: 'ki', many: 'ków', fraction: 'ka' }, // słoik
	{ suffix: 'g', few: 'gi', many: 'gów', fraction: 'ga' },
	{ suffix: 'r', few: 'ry', many: 'rów', fraction: 'ra' },
	{ suffix: 't', few: 'ty', many: 'tów', fraction: 'ta' },
	{ suffix: 'l', few: 'le', many: 'li', fraction: 'la' }
];

/** Custom units (PROJECT §5.6): the picker entry and the "Nowa jednostka" dialog. */
export const customUnits = {
	newUnit: '+ Nowa jednostka…',
	productUnitHint: 'tylko dla tego produktu',
	dialog: {
		title: 'Nowa jednostka',
		name: 'Nazwa',
		namePlaceholder: 'np. puszka',
		formsHeading: 'Odmiana',
		formsHint: 'Sprawdź i popraw formy, jeśli trzeba.',
		forms: {
			one: 'Jedna (1)',
			few: 'Kilka (2)',
			many: 'Wiele (5)',
			fraction: 'Ułamek (½)'
		},
		example: (f: { one: string; few: string; many: string; fraction: string }) =>
			`1 ${f.one} · 2 ${f.few} · 5 ${f.many} · ½ ${f.fraction}`,
		onlyFor: (product: string) => `Tylko dla produktu ${product}`,
		onlyForHint: 'Wyłącz, aby jednostka była dostępna dla każdego produktu.',
		equals: (name: string) => `1 ${name || 'jednostka'} =`,
		value: 'Ilość',
		unit: 'Jednostka',
		submit: 'Dodaj jednostkę',
		created: (name: string) => `Dodano jednostkę „${name}”`,
		required: 'Uzupełnij to pole',
		tooLong: 'Najwyżej 40 znaków',
		valueRequired: 'Podaj ilość większą od zera',
		incompatible: 'Tej jednostki nie da się przeliczyć dla wybranego produktu'
	}
} as const;
