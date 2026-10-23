// Shared UI strings. Additive only (plan §4.2); F0 owns the wording of the shell.
export const common = {
	appName: 'Przepiśnik',
	nav: {
		label: 'Nawigacja główna',
		recipes: 'Przepisy',
		fridge: 'Lodówka',
		shopping: 'Lista zakupów',
		profile: 'Profil'
	},
	loading: 'Ładowanie…',
	underConstruction: 'Ta strona jest jeszcze w budowie.',
	retry: 'Spróbuj ponownie',
	errorTitle: 'Coś poszło nie tak',

	// Generic actions, reused by every feature (DESIGN.md "Wording").
	actions: {
		back: 'Wróć',
		close: 'Zamknij',
		cancel: 'Anuluj',
		save: 'Zapisz',
		delete: 'Usuń',
		confirm: 'Potwierdź',
		undo: 'Cofnij',
		add: 'Dodaj',
		edit: 'Edytuj',
		more: 'Więcej'
	},

	// App shell (src/lib/components/shell).
	shell: {
		skipToContent: 'Przejdź do treści',
		tagline: 'Zeszyt z przepisami'
	},

	// Texts built into the UI primitives (src/lib/components/ui).
	ui: {
		optional: 'opcjonalnie',
		required: 'wymagane',
		notANumber: 'Wpisz liczbę, np. 1,5.',
		numberMin: (min: string) => `Najmniejsza wartość to ${min}.`,
		numberMax: (max: string) => `Największa wartość to ${max}.`,
		selectPlaceholder: 'Wybierz…',
		searchPlaceholder: 'Szukaj…',
		searching: 'Szukam…',
		noResults: 'Nic nie znaleziono.',
		createNew: (query: string) => `Dodaj „${query}”`,
		removeTag: (tag: string) => `Usuń „${tag}”`,
		tagInputHint: 'Zatwierdź Enterem lub przecinkiem.',
		notifications: 'Powiadomienia',
		dismiss: 'Zamknij powiadomienie',
		percent: (value: string) => `${value} %`,
		avatarOf: (name: string) => `Awatar: ${name}`
	},

	// Texts of the dev-only showcase at /ui (src/routes/(dev)/ui). Not shown to users in production.
	showcase: {
		title: 'Elementy interfejsu',
		intro: 'Każdy element w każdym stanie. Strona widoczna tylko w trybie deweloperskim.',
		sections: {
			buttons: 'Przyciski',
			fields: 'Pola formularza',
			choices: 'Wybór',
			feedback: 'Komunikaty',
			overlays: 'Okna i arkusze',
			misc: 'Pozostałe'
		},
		sampleTitle: 'Żurek na zakwasie',
		sampleText: 'Zakwas wlej powoli, cały czas mieszając, żeby się nie zważył.',
		ingredient: 'Składnik',
		ingredientHint: 'Zacznij pisać, np. „mleko”.',
		amount: 'Ilość',
		recipeName: 'Nazwa przepisu',
		notes: 'Notatki',
		unit: 'Jednostka',
		units: { g: 'gramy', ml: 'mililitry', pcs: 'sztuki' },
		tags: 'Tagi',
		share: 'Udostępnij w lodówce',
		shareHint: 'Domownicy zobaczą ten przepis.',
		remaining: 'Ile zostało',
		disabled: 'Niedostępne',
		loadingButton: 'Zapisywanie',
		withError: 'Z błędem',
		errorSample: 'Nazwa jest za krótka.',
		shortName: 'Ż',
		emptyTitle: 'Lodówka jest pusta',
		emptyText: 'Dodaj pierwszy produkt, a przepisy pokażą, czego brakuje.',
		emptyAction: 'Dodaj produkt',
		openDialog: 'Otwórz okno',
		openSheet: 'Wybierz opcję',
		openConfirm: 'Usuń przepis',
		confirmTitle: 'Usunąć przepis?',
		confirmText: 'Tej operacji nie można cofnąć.',
		dialogTitle: 'Odejmij z lodówki',
		dialogText: 'Zaznacz produkty, które zużyłeś.',
		sheetTitle: 'Zamiennik składnika',
		sheetOptions: ['Śmietana 18%', 'Jogurt grecki', 'Bez zamiennika'],
		showToast: 'Pokaż powiadomienie',
		showUndoToast: 'Usuń i pozwól cofnąć',
		toastSaved: 'Zapisano przepis.',
		toastDeleted: 'Usunięto produkt.',
		toastRestored: 'Przywrócono produkt.',
		tabs: { all: 'Wszystkie', mine: 'Moje przepisy', variants: 'Moje warianty' },
		tabPanel: (name: string) => `Zawartość zakładki „${name}”.`,
		filters: 'Filtry',
		chipSamples: ['obiad', 'wegetariańskie', 'szybkie'],
		products: ['mleko', 'mleko kokosowe', 'masło', 'mąka pszenna', 'marchew', 'majeranek'],
		created: (name: string) => `Utworzono „${name}”.`,
		nicknames: ['ania', 'Bartek Kowalski']
	}
} as const;
