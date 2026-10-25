// Recipe catalog, view and editor (owned by F4; the view strings by F5 live in variants.ts).
function variantsPart(total: number, foreign: number): string {
	if (foreign <= 0) return String(total);
	const others = foreign === 1 ? '1 innej osoby' : `${foreign} innych osób`;
	return `${total}, w tym ${others}`;
}

export const recipes = {
	title: 'Przepisy',
	newTitle: 'Nowy przepis',
	editTitle: 'Edytuj przepis',
	detailTitle: 'Przepis',

	catalog: {
		search: 'Szukaj przepisów',
		searchPlaceholder: 'Nazwa przepisu',
		tagFilter: 'Filtruj po tagach',
		tabsLabel: 'Widok katalogu',
		tabs: { all: 'Wszystkie', mine: 'Moje przepisy', variants: 'Moje warianty' },
		subLabel: 'Moje przepisy',
		sub: { created: 'Utworzone', favorites: 'Ulubione' },
		newRecipe: 'Nowy przepis',
		import: 'Importuj',
		showMore: 'Pokaż więcej',
		loadError: 'Nie udało się wczytać przepisów.',
		emptyAllTitle: 'Brak przepisów',
		emptyAllText: 'Dodaj pierwszy przepis albo zmień filtry.',
		emptySearchTitle: 'Nic nie znaleziono',
		emptySearchText: 'Spróbuj innej nazwy albo usuń filtry.',
		emptyCreatedTitle: 'Nie masz jeszcze własnych przepisów',
		emptyCreatedText: 'Zapisz pierwszy przepis, a pojawi się tutaj.',
		emptyFavoritesTitle: 'Brak ulubionych',
		emptyFavoritesText: 'Dotknij serca przy przepisie lub wariancie, aby go tu zapisać.',
		emptyVariantsTitle: 'Nie masz jeszcze wariantów',
		emptyVariantsText: 'Otwórz przepis i utwórz jego wariant.',
		variantCount: {
			one: (n: number) => `${n} wariant`,
			few: (n: number) => `${n} warianty`,
			many: (n: number) => `${n} wariantów`,
			fraction: (n: number) => `${n} wariantu`
		},
		by: (nick: string) => `autor: ${nick}`,
		prepTime: (m: number) => `Przygotowanie ${m} min`,
		cookTime: (m: number) => `Gotowanie ${m} min`,
		noCover: 'Brak zdjęcia',
		variantOf: (variant: string, recipe: string) => `wariant „${variant}” · przepis „${recipe}”`,
		baseUnavailable: 'Przepis bazowy jest niedostępny',
		deleteVariant: 'Usuń wariant',
		deleteVariantTitle: 'Usunąć wariant?',
		deleteVariantText: (name: string) => `Wariant „${name}” zostanie usunięty.`,
		variantDeleted: 'Usunięto wariant.',
		favoriteRemoved: 'Usunięto z ulubionych.',
		favoriteFailed: 'Nie udało się zmienić ulubionych.'
	},

	visibility: {
		label: 'Widoczność',
		private: 'Prywatny',
		fridges: 'Moje lodówki',
		public: 'Publiczny',
		privateHint: 'Widzisz go tylko ty.',
		fridgesHint: 'Widzisz go ty i osoby, z którymi dzielisz lodówkę.',
		publicHint: 'Widzą go wszyscy użytkownicy.',
		confirmPublicTitle: 'Zrobić przepis publicznym?',
		confirmPublicText:
			'Publicznego przepisu nie można edytować — zmiany będą możliwe tylko przez warianty.',
		confirmPublic: 'Zrób publicznym',
		iconLabel: {
			private: 'Prywatny',
			fridges: 'Widoczny w moich lodówkach',
			public: 'Publiczny'
		}
	},

	favorite: {
		add: 'Dodaj do ulubionych',
		remove: 'Usuń z ulubionych'
	},

	changeVisibility: {
		title: 'Zmień widoczność',
		save: 'Zmień widoczność',
		saved: 'Zmieniono widoczność.',
		locked: 'Nie można zawęzić widoczności — inni użytkownicy mają warianty tego przepisu.',
		lockedCount: (n: number) => `Warianty innych osób: ${n}.`
	},

	editor: {
		sectionBasics: 'Podstawowe informacje',
		sectionIngredients: 'Składniki',
		sectionPrep: 'Przygotowanie',
		sectionInstructions: 'Wykonanie',
		title: 'Tytuł',
		description: 'Opis',
		prepMinutes: 'Czas przygotowania',
		cookMinutes: 'Czas gotowania',
		minutes: 'min',
		servings: 'Liczba porcji',
		servingsHint: 'Pozwala przeliczyć przepis na inną liczbę osób.',
		tags: 'Tagi',
		tagsHint: 'Zatwierdź Enterem lub przecinkiem. Tag „szybkie” dodajemy sami.',
		utensils: 'Przybory',
		utensilsHint: 'Np. piekarnik, blender. Zatwierdź Enterem lub przecinkiem.',
		cover: 'Zdjęcie główne',
		addCover: 'Dodaj zdjęcie',
		changeCover: 'Zmień zdjęcie',
		removeCover: 'Usuń zdjęcie główne',
		coverAlt: 'Zdjęcie główne przepisu',
		save: 'Zapisz przepis',
		saveVariant: 'Zapisz wariant',
		cancel: 'Anuluj',
		saved: 'Zapisano przepis.',
		created: 'Dodano przepis.',
		saveFailed: 'Nie udało się zapisać przepisu.',
		leaveTitle: 'Porzucić zmiany?',
		leaveText: 'Masz niezapisane zmiany. Jeśli wyjdziesz, zostaną utracone.',
		leaveConfirm: 'Porzuć zmiany',
		variantsBanner: (total: number, foreign: number) =>
			`Zapisanie zmian usunie wszystkie warianty tego przepisu (${variantsPart(total, foreign)}).`,
		variantsTitle: 'Usunąć warianty?',
		variantsConfirmText: (total: number, foreign: number) =>
			`Zapisanie zmian usunie wszystkie warianty tego przepisu (${variantsPart(total, foreign)}). Kontynuować?`,
		variantsConfirm: 'Zapisz i usuń warianty',
		form: 'Formularz przepisu'
	},

	ingredients: {
		row: (n: number) => `Składnik ${n}`,
		product: 'Produkt',
		productPlaceholder: 'Zacznij pisać nazwę produktu',
		quantity: 'Ilość',
		unit: 'Jednostka',
		size: 'Wielkość',
		toTaste: 'Do smaku',
		toTasteHint: 'Bez ilości — nie jest skalowany ani odejmowany z lodówki.',
		optional: 'Opcjonalny',
		note: 'Notatka',
		notePlaceholder: 'np. pokrojona w kostkę',
		add: 'Dodaj składnik',
		insertBelow: 'Wstaw składnik poniżej',
		moveUp: 'Przesuń w górę',
		moveDown: 'Przesuń w dół',
		drag: 'Przeciągnij, aby zmienić kolejność',
		remove: 'Usuń składnik',
		removeInVariant: 'Usuń w wariancie',
		restore: 'Przywróć',
		removedInVariant: 'Usunięty w wariancie',
		unitLabels: {
			g: 'g',
			kg: 'kg',
			ml: 'ml',
			l: 'l',
			glass: 'szklanka',
			tbsp: 'łyżka',
			tsp: 'łyżeczka',
			drop: 'kropla',
			pinch: 'szczypta',
			piece: 'szt.'
		},
		sizeLabels: { small: 'mała', medium: 'średnia', large: 'duża' }
	},

	steps: {
		step: (n: number) => `Krok ${n}`,
		text: 'Tekst kroku',
		textPlaceholder: 'Opisz ten krok',
		addStep: 'Dodaj krok',
		insertImage: 'Wstaw zdjęcie',
		removeStep: 'Usuń krok',
		removeStepInVariant: 'Usuń krok w wariancie',
		restoreStep: 'Przywróć krok',
		removedInVariant: 'Krok usunięty w wariancie',
		moveStepUp: 'Przesuń krok w górę',
		moveStepDown: 'Przesuń krok w dół',
		caption: 'Podpis zdjęcia',
		removeImage: 'Usuń zdjęcie',
		moveImageUp: 'Przesuń zdjęcie w górę',
		moveImageDown: 'Przesuń zdjęcie w dół',
		imageAlt: 'Zdjęcie w kroku',
		uploading: (percent: number) => `Wysyłanie zdjęcia: ${percent} %`,
		prepEmpty: 'Brak kroków przygotowania.',
		instructionsEmpty: 'Dodaj przynajmniej jeden krok.'
	},

	newProduct: {
		title: 'Dodaj produkt',
		name: 'Nazwa produktu',
		category: 'Kategoria',
		measureKind: 'Jak mierzymy produkt',
		displayClass: 'Jak pokazywać ilość',
		hasSizes: 'Ma małe, średnie i duże sztuki',
		allowHalf: 'Można użyć połówki',
		gramsPerMl: 'Gęstość',
		gramsPerMlHint: 'Gramy w 1 ml. Potrzebna, by przeliczać gramy na łyżki i odwrotnie.',
		gramsPerPiece: 'Waga sztuki',
		gramsPerPieceHint: 'Gramy w jednej średniej sztuce. Potrzebna, by przeliczać sztuki na gramy.',
		defaultPackageAmount: 'Typowe opakowanie',
		submit: 'Dodaj produkt',
		created: (name: string) => `Dodano produkt „${name}”.`,
		existing: (name: string) => `Taki produkt już jest: „${name}”.`,
		userAdded: 'dodany przez użytkownika',
		unknownTitle: (name: string) => `„${name}” nie ma w bazie produktów. Dodać?`,
		unknownDescription: 'Produkt trafi do wspólnej bazy, z której korzystają wszyscy.',
		unknownAdd: 'Dodaj do bazy',
		unknownDecline: 'Nie dodawaj',
		categories: {
			vegetables: 'warzywa',
			fruits: 'owoce',
			meat: 'mięso',
			fish: 'ryby',
			dairy: 'nabiał',
			bakery: 'pieczywo',
			dryGoods: 'sypkie',
			spices: 'przyprawy',
			oilsVinegars: 'oleje i octy',
			sauces: 'sosy',
			canned: 'konserwy',
			frozen: 'mrożonki',
			drinks: 'napoje',
			sweets: 'słodycze',
			other: 'inne'
		},
		measureKinds: {
			mass: 'Waga (gramy)',
			volume: 'Objętość (mililitry)',
			count: 'Sztuki'
		},
		displayClasses: {
			weight: 'Waga',
			liquid: 'Płyn',
			spice: 'Przyprawa',
			piece: 'Sztuki'
		}
	},

	readOnly: {
		publicTitle: 'Publicznego przepisu nie można edytować',
		publicText: 'Zmiany są możliwe tylko przez warianty. Utwórz własny wariant tego przepisu.',
		createVariant: 'Utwórz wariant',
		archivedTitle: 'Ten przepis jest zarchiwizowany',
		archivedText: 'Jest tylko do odczytu, bo inni mają jego warianty.',
		notAuthorTitle: 'Nie możesz edytować tego przepisu',
		notAuthorText: 'Tylko autor może go zmienić.',
		backToRecipe: 'Wróć do przepisu'
	},

	delete: {
		action: 'Usuń przepis',
		title: 'Usunąć przepis?',
		text: 'Przepis zostanie usunięty na stałe.',
		textArchive: 'Inni mają warianty tego przepisu, więc zostanie ukryty, a nie usunięty.',
		confirm: 'Usuń przepis',
		deleted: 'Usunięto przepis.',
		archived: 'Przepis zostanie ukryty, bo inni mają jego warianty.'
	},

	transfer: {
		exportAction: 'Eksportuj',
		exportFailed: 'Nie udało się wyeksportować przepisu.',
		importTitle: 'Importuj przepis',
		importText:
			'Wybierz plik JSON wyeksportowany z Przepiśnika. Przepis i jego warianty trafią do ciebie jako prywatne.',
		importFile: 'Plik z przepisem',
		importSubmit: 'Importuj',
		imported: 'Zaimportowano przepis.',
		notJson: 'Ten plik nie jest poprawnym plikiem JSON.',
		// Import of a recipe that already exists (PROJECT §4.7, D-35).
		existsTitle: 'Ten przepis już istnieje',
		existsText:
			'Masz dostęp do przepisu o tym samym tytule. Możesz dodać plik jako jego warianty — prywatne, widoczne tylko dla ciebie.',
		matchesLabel: 'Przepisy o tym tytule',
		addAsVariant: 'Dodaj jako wariant',
		matchAuthor: (nickname: string) => `@${nickname}`,
		importAsNew: 'Importuj jako nowy przepis',
		formatHelp: 'Format pliku',
		importedAsVariants: 'Dodano warianty z pliku.',
		importedAsVariantsSkipped: (skipped: number) =>
			`Dodano warianty z pliku (pominięto istniejące: ${skipped}).`,
		allSkipped: (skipped: number) => `Te warianty już istnieją (pominięto ${skipped})`
	},

	upload: {
		invalidType: 'Dozwolone są tylko zdjęcia JPEG, PNG lub WebP.',
		tooLarge: 'Zdjęcie jest za duże (maksymalnie 10 MB).',
		failed: 'Nie udało się wysłać zdjęcia.'
	},

	// Client-side field errors; the keys mirror the API's short codes (RecipeInputValidator).
	fieldErrors: {
		required: 'To pole jest wymagane.',
		too_long: 'Tekst jest za długi.',
		out_of_range: 'Wartość spoza dozwolonego zakresu.',
		minutesRange: 'Podaj liczbę minut od 0 do 2880.',
		servingsRange: 'Podaj liczbę porcji od 1 do 100.',
		productRequired: 'Wybierz produkt.',
		quantityRequired: 'Podaj ilość większą od zera.',
		unitIncompatible: 'Tej jednostki nie można użyć dla tego produktu.',
		ingredientsRange: 'Dodaj od 1 do 100 składników.',
		instructionsRange: 'Dodaj od 1 do 50 kroków.',
		prepRange: 'Maksymalnie 50 kroków.',
		stepEmpty: 'Krok nie może być pusty.',
		'quantity.unit_incompatible': 'Tej jednostki nie można użyć dla tego produktu.',
		'quantity.invalid': 'Nieprawidłowa ilość.',
		'image.not_owned': 'Możesz użyć tylko własnych zdjęć.',
		generic: 'Popraw to pole.'
	}
} as const;

/** "porcja" in the four plural forms (PROJECT §5.5, §5.7): 1 porcja, 3 porcje, 6 porcji, 2 ½ porcji. */
export const servingsForms = {
	one: 'porcja',
	few: 'porcje',
	many: 'porcji',
	fraction: 'porcji'
} as const;
