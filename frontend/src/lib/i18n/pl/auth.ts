// Login, onboarding and the invite landing page (owned by F3).
export const auth = {
	loginTitle: 'Logowanie',
	loginIntro: 'Zaloguj się, aby zapisywać przepisy i dzielić lodówkę z domownikami.',
	googleLoading: 'Ładuję logowanie Google…',
	googleFailed: 'Nie udało się załadować logowania Google. Sprawdź połączenie i spróbuj ponownie.',
	googleRetry: 'Spróbuj ponownie',
	notConfigured: 'Logowanie nie jest jeszcze skonfigurowane. Skontaktuj się z administratorem.',
	devLogin: {
		title: 'Logowanie deweloperskie',
		hint: 'Dostępne tylko w trybie deweloperskim. Pozwala zalogować się jako dowolny użytkownik testowy.',
		email: 'E-mail',
		nickname: 'Pseudonim',
		submit: 'Zaloguj się'
	},
	welcomeTitle: 'Witaj!',
	welcomeIntro: 'Wybierz pseudonim, pod którym inni zobaczą Twoje przepisy.',
	welcomeSubmit: 'Zacznij gotować',
	logout: 'Wyloguj',
	nickname: {
		label: 'Pseudonim',
		rules: 'Od 3 do 30 znaków: litery, cyfry, _ lub -.',
		tooShort: 'Pseudonim musi mieć co najmniej 3 znaki.',
		tooLong: 'Pseudonim może mieć najwyżej 30 znaków.',
		badChars: 'Użyj tylko liter, cyfr, _ lub -, bez spacji.',
		checking: 'Sprawdzam, czy pseudonim jest wolny…',
		available: 'Pseudonim jest wolny.'
	},
	invite: {
		expiredTitle: 'To zaproszenie wygasło',
		expiredText: 'Link jest nieaktualny albo właściciel go cofnął. Poproś o nowy.',
		toFridges: 'Moje lodówki'
	}
} as const;
