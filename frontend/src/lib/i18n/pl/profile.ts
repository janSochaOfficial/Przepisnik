// Profile and settings (owned by F3).
export const profile = {
	title: 'Profil',
	loadFailed: 'Nie udało się wczytać ustawień.',
	nickname: {
		title: 'Pseudonim',
		save: 'Zapisz pseudonim',
		saved: 'Zapisano pseudonim.'
	},
	units: {
		title: 'Jednostki płynów',
		group: 'Jednostki płynów w przepisach',
		kitchen: 'Szklanki i łyżki',
		metric: 'Mililitry',
		example: (text: string) => `Przykład: „${text} mleka”.`,
		saved: 'Zapisano ustawienia.'
	},
	fridge: {
		title: 'Domyślna lodówka',
		label: 'Lodówka wybierana jako pierwsza',
		hint: 'Używana przy odejmowaniu z lodówki i na liście zakupów.',
		none: 'Brak (zapytaj za każdym razem)'
	},
	importRecipe: 'Importuj przepis',
	importHint: 'Wczytaj przepis zapisany wcześniej w pliku JSON.',
	logout: 'Wyloguj się'
} as const;
