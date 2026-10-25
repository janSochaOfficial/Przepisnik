// Recipe view, auto-match and variant editor (owned by F5).
export const variants = {
	newTitle: 'Nowy wariant',
	editTitle: 'Edytuj wariant',

	view: {
		loadError: 'Nie udało się wczytać przepisu.',
		baseName: 'Przepis podstawowy',
		customMix: 'Własna kombinacja',
		customMixHint:
			'Wybrane składniki nie pasują do żadnego jednego wariantu. Kroki pochodzą z najbliższego.',
		activeVariant: 'Aktywny wariant',
		by: (nick: string) => `autor: ${nick}`,
		prepTime: (m: number) => `Przygotowanie: ${m} min`,
		cookTime: (m: number) => `Gotowanie: ${m} min`,
		utensils: 'Przybory',
		ingredients: 'Składniki',
		prep: 'Przygotowanie',
		instructions: 'Wykonanie',
		ingredientList: 'Lista składników',
		toTaste: 'do smaku',
		optional: 'opcjonalnie',
		removed: 'pominięty w tym wyborze',
		marker: {
			user: 'Twój wybór',
			auto: 'Auto'
		},
		stepsLoading: 'Wczytuję kroki wariantu…',
		stepsFailed: 'Nie udało się wczytać kroków wariantu.',
		showImage: 'Powiększ zdjęcie',
		closeImage: 'Zamknij zdjęcie',
		imageAlt: 'Zdjęcie w kroku',
		coverAlt: 'Zdjęcie przepisu',
		visibilityIcon: 'Widoczność przepisu'
	},

	menu: {
		open: 'Więcej działań',
		title: 'Działania',
		edit: 'Edytuj',
		createVariant: 'Utwórz wariant',
		newVariant: 'Nowy wariant',
		changeVisibility: 'Zmień widoczność',
		export: 'Eksportuj',
		delete: 'Usuń'
	},

	sheet: {
		title: (product: string) => `Składnik: ${product}`,
		variantsHeading: 'Warianty',
		scaleHeading: 'Przelicz',
		none: 'Brak składnika',
		selected: 'wybrane',
		supportedBy: 'Używają: ',
		favoriteVariant: 'ulubiony',
		createFromSelection: '+ Utwórz nowy wariant z bieżącego wyboru',
		have: 'Mam',
		haveUnit: 'Jednostka',
		haveSize: 'Wielkość',
		apply: 'Przelicz',
		reset: 'Przywróć',
		incompatible: 'Tej jednostki nie można przeliczyć dla tego produktu.',
		enterAmount: 'Podaj ilość większą od zera.'
	},

	scaling: {
		banner: (factor: string, anchor: string) => `Przeliczono ×${factor} według: ${anchor}`,
		reset: 'Przywróć'
	},

	servings: {
		label: 'Porcje:',
		input: 'Liczba porcji',
		decrease: 'Mniej porcji',
		increase: 'Więcej porcji',
		fromIngredient: (servings: string) => `Przeliczone składniki wystarczą na ${servings}.`,
		range: 'Podaj liczbę porcji od 1 do 100.'
	},

	clearSelection: 'Wyczyść wybór',
	selectionSaveFailed: 'Nie udało się zapisać twojego wyboru.',

	actions: {
		group: 'Działania na przepisie',
		deduct: 'Odejmij z lodówki',
		addMissing: 'Dodaj brakujące do listy'
	},

	tabs: {
		label: 'Widok przepisu',
		recipe: 'Przepis',
		variants: (n: number) => `Warianty (${n})`
	},

	list: {
		label: 'Warianty przepisu',
		base: 'Przepis bazowy',
		by: (nick: string) => `@${nick}`,
		cooked: (n: number) => `ugotowano: ${n}`,
		shown: 'wyświetlany',
		emptyTitle: 'Brak wariantów',
		emptyText: 'Nikt jeszcze nie dodał wariantu tego przepisu.',
		create: 'Utwórz wariant',
		favoriteAdd: (name: string) => `Dodaj wariant „${name}” do ulubionych`,
		favoriteRemove: (name: string) => `Usuń wariant „${name}” z ulubionych`
	},

	favorite: {
		recipeAdd: 'Dodaj przepis do ulubionych',
		recipeRemove: 'Usuń przepis z ulubionych',
		variantAdd: 'Dodaj aktywny wariant do ulubionych',
		variantRemove: 'Usuń aktywny wariant z ulubionych'
	},

	editor: {
		section: 'O wariancie',
		name: 'Nazwa wariantu',
		description: 'Opis',
		visibility: 'Widoczność wariantu',
		narrowerHint: 'Wariant zobaczą tylko osoby, które widzą przepis bazowy.',
		save: 'Zapisz wariant',
		created: 'Dodano wariant.',
		saved: 'Zapisano wariant.',
		nameRequired: 'Podaj nazwę wariantu.',
		nameTooLong: 'Nazwa może mieć najwyżej 80 znaków.',
		basedOn: (title: string) => `Wariant przepisu „${title}”`,
		delete: 'Usuń wariant',
		deleteTitle: 'Usunąć wariant?',
		deleteText: (name: string) => `Wariant „${name}” zostanie usunięty na stałe.`,
		deleted: 'Usunięto wariant.',
		conflict:
			'Wariant zmienił się w międzyczasie (np. po ugotowaniu). Wczytaliśmy go ponownie. Zapisz jeszcze raz.',
		loadError: 'Nie udało się wczytać wariantu.',
		notAuthor: 'Tylko autor może zmienić ten wariant.',
		baseUnavailable: 'Przepis bazowy jest niedostępny, można tylko usunąć wariant.',
		archived: 'Ten przepis jest zarchiwizowany, więc nie można tworzyć jego wariantów.',
		back: 'Wróć do przepisu'
	}
} as const;
