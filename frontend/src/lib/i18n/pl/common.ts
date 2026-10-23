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
	errorTitle: 'Coś poszło nie tak'
} as const;
