// Fridges, items, members, invites and the deduction dialog (owned by F6).
export const fridge = {
	title: 'Lodówka',
	inviteTitle: 'Zaproszenie do lodówki',
	loadFailed: 'Nie udało się wczytać lodówki.',

	switcher: {
		label: 'Lodówka',
		newFridge: 'Nowa lodówka',
		newTitle: 'Nowa lodówka',
		nameLabel: 'Nazwa lodówki',
		create: 'Utwórz lodówkę',
		created: 'Utworzono lodówkę.'
	},

	empty: {
		noFridgeTitle: 'Nie masz jeszcze lodówki',
		noFridgeText: 'Utwórz lodówkę, aby zapisywać w niej produkty i prowadzić listę zakupów.',
		title: 'Lodówka jest pusta',
		text: 'Dodaj pierwszy produkt, a przepisy pokażą, czego brakuje.',
		noResultsTitle: 'Brak wyników',
		noResultsText: 'Żaden produkt w tej lodówce nie pasuje do wyszukiwania.'
	},

	search: 'Szukaj w lodówce',
	items: 'Produkty w lodówce',
	categoryGroup: (name: string) => `Kategoria: ${name}`,

	item: {
		/** `Śmietanka 30%` + this line: `330 ml · zostało 70 % (~230 ml)`. */
		remaining: (percent: string, amount: string) => `zostało ${percent} (${amount})`,
		piecesLeft: (left: string, total: string) => `zostało ${left} z ${total}`,
		note: 'Notatka'
	},

	add: {
		button: 'Dodaj produkt',
		title: 'Dodaj do lodówki',
		packageLabel: 'Opakowanie',
		packageHint: 'Ile jest w całym opakowaniu.',
		percentLabel: 'Ile zostało',
		noteLabel: 'Notatka',
		productRequired: 'Wybierz produkt z listy.',
		packageRequired: 'Podaj, ile jest w opakowaniu.',
		submit: 'Dodaj do lodówki',
		added: 'Dodano do lodówki.'
	},

	edit: {
		title: (name: string) => name,
		packageLabel: 'Opakowanie',
		percentLabel: 'Ile zostało',
		piecesLabel: 'Ile sztuk zostało',
		noteLabel: 'Notatka',
		packageRequired: 'Podaj, ile jest w opakowaniu.',
		increase: 'Dodaj sztukę',
		decrease: 'Odejmij sztukę',
		save: 'Zapisz zmiany',
		saved: 'Zapisano zmiany.',
		delete: 'Usuń z lodówki',
		deleted: 'Usunięto produkt z lodówki.',
		restoreFailed: 'Nie udało się przywrócić produktu.',
		conflict: 'Ktoś zmienił ten produkt. Odświeżyłem lodówkę, spróbuj jeszcze raz.'
	},

	settings: {
		button: 'Ustawienia lodówki',
		title: 'Ustawienia lodówki',
		nameLabel: 'Nazwa lodówki',
		rename: 'Zmień nazwę',
		renamed: 'Zmieniono nazwę.',
		members: 'Domownicy',
		owner: 'właściciel',
		you: 'to Ty',
		removeMember: (name: string) => `Usuń: ${name}`,
		memberRemoved: (name: string) => `Usunięto: ${name}.`,
		leave: 'Opuść lodówkę',
		leaveTitle: 'Opuścić lodówkę?',
		leaveText: 'Stracisz dostęp do jej produktów i listy zakupów, dopóki ktoś Cię nie zaprosi.',
		left: 'Opuszczono lodówkę.',
		invites: 'Zaproszenia',
		invitesHint: 'Link działa 7 dni i można go użyć wielokrotnie.',
		createInvite: 'Utwórz zaproszenie',
		noInvites: 'Brak aktywnych zaproszeń.',
		inviteExpires: (date: string) => `ważne do ${date}`,
		copyLink: 'Kopiuj link',
		linkCopied: 'Skopiowano link zaproszenia.',
		copyFailed: 'Nie udało się skopiować linku.',
		shareLink: 'Udostępnij link',
		shareText: (fridgeName: string) => `Zapraszam do lodówki „${fridgeName}” w Przepiśniku.`,
		revoke: 'Cofnij zaproszenie',
		revoked: 'Cofnięto zaproszenie.',
		deleteFridge: 'Usuń lodówkę',
		deleteTitle: 'Usunąć lodówkę?',
		deleteText: 'Zniknie razem z produktami i listą zakupów, dla wszystkich domowników.',
		deleted: 'Usunięto lodówkę.'
	},

	join: {
		loadFailed: 'Nie udało się sprawdzić zaproszenia.',
		heading: (fridgeName: string) => `Zaproszenie do lodówki „${fridgeName}”`,
		from: (owner: string) => `Zaprasza: ${owner}`,
		expires: (date: string) => `Ważne do ${date}`,
		alreadyMember: 'Jesteś już w tej lodówce.',
		accept: 'Dołącz',
		open: 'Otwórz lodówkę',
		joined: 'Dołączono do lodówki.'
	},

	deduction: {
		open: 'Odejmij z lodówki',
		title: 'Odejmij z lodówki',
		description: 'Zaznacz, co zużyłeś(-aś) w tym przepisie.',
		fridgeLabel: 'Lodówka',
		rows: 'Składniki do odjęcia',
		needed: (amount: string) => `potrzeba ${amount}`,
		available: (amount: string) => `w lodówce ${amount}`,
		missing: 'Brak w lodówce',
		short: (amount: string) => `Za mało — jest ${amount}`,
		noRows: 'Ten przepis nie ma składników z ilością do odjęcia.',
		/** Shown while the 12-hour rule applies (PROJECT §9.1): the deduction still happens. */
		cookNotCounted:
			'Ten przepis był już liczony jako ugotowany w ciągu ostatnich 12 godzin, więc licznik się nie zwiększy.',
		loadFailed: 'Nie udało się wczytać lodówki.',
		deductSelected: 'Odejmij zaznaczone',
		deductAll: 'Odejmij wszystko',
		addMissing: 'Dodaj brakujące do listy zakupów',
		/** `n` products: 1 produkt, 2 produkty, 5 produktów. */
		deducted: (n: number, noun: string) => `Odjęto ${n} ${noun}`,
		products: { one: 'produkt', few: 'produkty', many: 'produktów', fraction: 'produktu' },
		undone: 'Cofnięto odjęcie.',
		nothingSelected: 'Zaznacz przynajmniej jeden składnik, który jest w lodówce.',
		undoFailed: 'Nie udało się cofnąć odjęcia.'
	}
} as const;
