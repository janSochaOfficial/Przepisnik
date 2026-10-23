// Polish messages for every API error code (API.md §9). Additive only (plan §4.2).
// errors.test.ts fails when a code listed in API.md §9 has no entry here.
export const errors = {
	'auth.invalid_token': 'Logowanie przez Google nie powiodło się. Spróbuj ponownie.',
	'auth.required': 'Zaloguj się, aby kontynuować.',
	'user.onboarding_required': 'Najpierw wybierz swój pseudonim.',
	'nickname.taken': 'Ten pseudonim jest już zajęty.',
	'nickname.invalid': 'Pseudonim musi mieć 3–30 znaków: litery, cyfry, _ lub -.',
	'admin.required': 'Ta operacja wymaga uprawnień administratora.',

	'product.not_found': 'Nie znaleziono produktu.',
	'product.duplicate': 'Taki produkt już istnieje.',
	'product.invalid_combination': 'Te ustawienia produktu wzajemnie się wykluczają.',
	'product.merge_incompatible': 'Nie można scalić produktów mierzonych w różny sposób.',
	'tag.reserved': 'Tego tagu nie można dodać ręcznie.',

	'image.invalid_type': 'Dozwolone są tylko zdjęcia JPEG, PNG lub WebP.',
	'image.too_large': 'Zdjęcie jest za duże (maksymalnie 10 MB).',
	'image.not_found': 'Nie znaleziono zdjęcia.',
	'image.not_owned': 'Możesz użyć tylko własnych zdjęć.',

	'recipe.not_found': 'Nie znaleziono przepisu.',
	'recipe.not_author': 'Tylko autor może zmienić ten przepis.',
	'recipe.public_locked':
		'Publicznego przepisu nie można edytować — zmiany są możliwe tylko przez warianty.',
	'recipe.has_variants': 'Ten przepis ma warianty. Zapisanie zmian je usunie.',
	'recipe.visibility_locked':
		'Nie można zawęzić widoczności — inni użytkownicy mają warianty tego przepisu.',
	'recipe.archived': 'Ten przepis jest zarchiwizowany i nie można go zmieniać.',
	'quantity.unit_incompatible': 'Tej jednostki nie można użyć dla tego produktu.',
	'quantity.invalid': 'Nieprawidłowa ilość.',

	'variant.not_found': 'Nie znaleziono wariantu.',
	'variant.not_author': 'Tylko autor może zmienić ten wariant.',
	'variant.no_changes': 'Wariant nie różni się od przepisu bazowego.',
	'variant.name_taken': 'Masz już wariant o tej nazwie.',
	'selection.unknown_slot': 'Wybór dotyczy składnika, którego już nie ma.',

	'fridge.not_found': 'Nie znaleziono lodówki.',
	'fridge.owner_required': 'Tylko właściciel lodówki może to zrobić.',
	'fridge.owner_cannot_leave': 'Właściciel nie może opuścić swojej lodówki — może ją usunąć.',
	'invite.not_found': 'Zaproszenie wygasło lub zostało cofnięte.',
	'fridge_item.not_found': 'Nie znaleziono tego produktu w lodówce.',
	'deduction.empty': 'Nie wybrano żadnych produktów do odjęcia.',
	'deduction.not_found': 'Nie znaleziono tego odjęcia.',
	'deduction.items_changed': 'Nie można cofnąć — produkty w lodówce zmieniły się w międzyczasie.',
	'deduction.already_undone': 'To odjęcie zostało już cofnięte.',
	'shopping_item.not_found': 'Nie znaleziono pozycji na liście zakupów.',
	'shopping_item.invalid': 'Nieprawidłowa pozycja listy zakupów.',

	'import.invalid_format': 'Ten plik nie jest prawidłowym eksportem przepisu.',
	'import.unsupported_version': 'Ta wersja pliku eksportu nie jest obsługiwana.',
	'import.too_large': 'Plik jest za duży (maksymalnie 25 MB).',

	'concurrency.conflict': 'Ktoś zmienił te dane w międzyczasie. Odśwież i spróbuj ponownie.',
	'validation.failed': 'Popraw zaznaczone pola.',
	'request.missing_csrf_header': 'Nieprawidłowe żądanie. Odśwież stronę.',
	'request.not_found': 'Nie znaleziono.',
	'request.forbidden': 'Brak dostępu.',
	'request.invalid': 'Nieprawidłowe żądanie.',
	rate_limited: 'Za dużo prób. Odczekaj chwilę.',
	'server.error': 'Coś poszło nie tak. Spróbuj ponownie później.',

	// Client-only: the request never reached the server.
	'network.error': 'Brak połączenia z serwerem.'
} as const satisfies Record<string, string>;

export type ErrorCode = keyof typeof errors;

/** The Polish message for an error code, falling back to the generic server error. */
export function errorMessage(code: string): string {
	return code in errors ? errors[code as ErrorCode] : errors['server.error'];
}
