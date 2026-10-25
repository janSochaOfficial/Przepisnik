// "Format pliku" help page for the recipe import (PROJECT §4.7, D-40). The field tables follow
// docs/API.md §8 and the `RecipeExport` schema in docs/api/openapi.yaml; the example is checked
// against that schema by src/lib/i18n/import-format.test.ts.

export type FormatField = { field: string; meaning: string; required: string; limits: string };
export type FormatTable = { id: string; heading: string; rows: FormatField[] };

export const importFormatExample = {
	format: 'recipes-list/recipe',
	formatVersion: 1,
	exportedAt: '2026-10-24T10:00:00Z',
	products: [
		{
			key: 'p1',
			name: 'Jajko',
			category: 'dairy',
			measureKind: 'count',
			displayClass: 'piece',
			hasSizes: true,
			allowHalf: false,
			gramsPerMl: null,
			gramsPerPiece: 60,
			defaultPackageAmount: 10,
			pieceForms: null
		},
		{
			key: 'p2',
			name: 'Mleko',
			category: 'dairy',
			measureKind: 'volume',
			displayClass: 'liquid',
			hasSizes: false,
			allowHalf: false,
			gramsPerMl: 1.03,
			gramsPerPiece: null,
			defaultPackageAmount: 1000,
			pieceForms: null
		}
	],
	images: {},
	recipe: {
		key: 'r1',
		title: 'Jajecznica na mleku',
		description: null,
		coverImageKey: null,
		authorNickname: 'kuchnia',
		prepMinutes: 2,
		cookMinutes: 5,
		servings: 2,
		tags: [],
		utensils: ['Patelnia'],
		ingredients: [
			{
				key: 'i1',
				productKey: 'p1',
				quantity: { value: 3, unit: 'piece' },
				note: null,
				optional: false
			},
			{
				key: 'i2',
				productKey: 'p2',
				quantity: { value: 50, unit: 'ml' },
				note: null,
				optional: false
			}
		],
		prepSteps: [],
		instructionSteps: [
			{
				key: 's1',
				blocks: [
					{
						type: 'text',
						text: 'Jajka roztrzep z mlekiem i smaż na patelni na małym ogniu, mieszając.'
					}
				]
			}
		]
	},
	variants: [
		{
			name: 'Więcej mleka',
			description: null,
			authorNickname: 'kuchnia',
			prepMinutes: null,
			cookMinutes: null,
			utensilsAdded: [],
			utensilsRemoved: [],
			ingredientOps: [
				{
					op: 'modify',
					targetKey: 'i2',
					productKey: 'p2',
					quantity: { value: 100, unit: 'ml' },
					note: null,
					optional: false
				}
			],
			stepOps: []
		}
	]
};

const field = (name: string, meaning: string, required: string, limits = '—'): FormatField => ({
	field: name,
	meaning,
	required,
	limits
});

export const importFormat = {
	title: 'Format pliku importu',
	back: 'Wróć do przepisów',
	copy: 'Kopiuj',
	copied: 'Skopiowano przykład do schowka.',
	copyFailed: 'Nie udało się skopiować przykładu.',
	intro:
		'Przepiśnik zapisuje przepis do pliku JSON i wczytuje go z powrotem. Ta strona opisuje budowę tego pliku, żebyś mógł sprawdzić go lub przygotować w innym programie.',
	columns: {
		field: 'Pole',
		meaning: 'Znaczenie',
		required: 'Wymagane',
		limits: 'Ograniczenia'
	},
	file: {
		heading: 'Co zawiera plik',
		items: [
			'Jeden przepis, jego warianty (te, które eksportujący może zobaczyć) i zdjęcia osadzone w pliku.',
			'Zdjęcia są zapisane jako tekst base64. Zajmują więcej miejsca niż oryginalne pliki.',
			'Plik może mieć najwyżej 25 MB.',
			'Pole formatVersion ma wartość 1. Plik z inną wersją formatu zostanie odrzucony.',
			'Plik jest zwykłym tekstem w kodowaniu UTF-8 i zawiera tylko dane przepisu, bez haseł ani adresów e-mail.'
		]
	},
	structure: {
		heading: 'Budowa pliku',
		intro: 'Najwyższy poziom pliku to jeden obiekt z poniższymi polami.',
		rows: [
			field('format', 'Stały napis rozpoznający plik.', 'tak', 'recipes-list/recipe'),
			field('formatVersion', 'Wersja formatu.', 'tak', 'liczba 1'),
			field(
				'exportedAt',
				'Data i godzina eksportu.',
				'tak',
				'data ISO 8601, np. 2026-10-24T10:00:00Z'
			),
			field(
				'products',
				'Produkty użyte w przepisie i wariantach. Każdy produkt występuje raz.',
				'tak',
				'lista, może być pusta'
			),
			field(
				'customUnits',
				'Własne jednostki użyte w pliku.',
				'nie',
				'brak, gdy plik nie używa własnych jednostek'
			),
			field('images', 'Zdjęcia: klucz zdjęcia → zdjęcie.', 'tak', 'obiekt, może być pusty: {}'),
			field('recipe', 'Przepis.', 'tak', 'obiekt Przepis, tabela poniżej'),
			field('variants', 'Warianty przepisu.', 'tak', 'lista, może być pusta')
		]
	},
	objects: [
		{
			id: 'object-recipe',
			heading: 'Przepis (recipe)',
			rows: [
				field('key', 'Klucz przepisu, unikalny w pliku.', 'tak', 'tekst'),
				field('title', 'Tytuł przepisu.', 'tak', '1–150 znaków'),
				field('description', 'Opis przepisu.', 'nie', 'do 2000 znaków'),
				field(
					'coverImageKey',
					'Klucz zdjęcia okładki z pola images.',
					'nie',
					'musi istnieć w images'
				),
				field('authorNickname', 'Pseudonim autora oryginału, bez znaku @.', 'tak', 'tekst'),
				field('prepMinutes', 'Czas przygotowania w minutach.', 'nie', 'liczba całkowita 0–2880'),
				field('cookMinutes', 'Czas gotowania w minutach.', 'nie', 'liczba całkowita 0–2880'),
				field('servings', 'Liczba porcji.', 'nie', 'liczba całkowita 1–100'),
				field(
					'tags',
					'Nazwy tagów. Tagi systemowe (np. „szybkie”) nie są eksportowane.',
					'tak',
					'lista nazw, może być pusta'
				),
				field('utensils', 'Nazwy naczyń kuchennych.', 'tak', 'lista nazw, może być pusta'),
				field('ingredients', 'Składniki przepisu.', 'tak', 'od 1 do 100 pozycji'),
				field('prepSteps', 'Kroki przygotowania.', 'tak', 'lista, do 50 kroków, może być pusta'),
				field('instructionSteps', 'Kroki wykonania.', 'tak', 'od 1 do 50 kroków')
			]
		},
		{
			id: 'object-ingredient',
			heading: 'Składnik (ingredients)',
			rows: [
				field('key', 'Klucz składnika, unikalny w pliku.', 'tak', 'tekst'),
				field('productKey', 'Klucz produktu z pola products.', 'tak', 'musi istnieć w products'),
				field(
					'quantity',
					'Ilość. Brak ilości oznacza „do smaku”.',
					'nie',
					'obiekt Ilość, tabela poniżej'
				),
				field('note', 'Uwaga do składnika, np. „drobno posiekana”.', 'nie', 'do 300 znaków'),
				field('optional', 'Czy składnik jest opcjonalny.', 'tak', 'true lub false')
			]
		},
		{
			id: 'object-quantity',
			heading: 'Ilość i jednostka (quantity)',
			rows: [
				field('value', 'Wartość ilości.', 'tak', 'liczba większa od zera'),
				field('unit', 'Jednostka, lista poniżej.', 'tak', 'jedna z wartości w tabeli jednostek'),
				field(
					'size',
					'Wielkość sztuki: mała, średnia lub duża.',
					'nie',
					'small, medium, large; tylko dla produktów z rozmiarami'
				),
				field(
					'customUnitKey',
					'Klucz własnej jednostki z pola customUnits.',
					'tak, gdy unit to custom',
					'w innym przypadku brak pola'
				)
			]
		},
		{
			id: 'object-steps',
			heading: 'Kroki i bloki treści (prepSteps, instructionSteps, blocks)',
			rows: [
				field('key', 'Klucz kroku, unikalny w pliku.', 'tak', 'tekst'),
				field(
					'blocks',
					'Treść kroku: bloki tekstu i zdjęć, w kolejności.',
					'tak',
					'lista, może być pusta'
				),
				field('type: text', 'Blok tekstu, pole text.', 'tak, dla bloku tekstu', 'do 5000 znaków'),
				field(
					'type: image',
					'Blok zdjęcia: imageKey (klucz z images) i opcjonalny caption.',
					'tak, dla bloku zdjęcia',
					'caption do 300 znaków albo null'
				)
			]
		},
		{
			id: 'object-variant',
			heading: 'Wariant (variants)',
			rows: [
				field('name', 'Nazwa wariantu.', 'tak', '1–80 znaków'),
				field('description', 'Opis wariantu.', 'nie', 'do 2000 znaków'),
				field('authorNickname', 'Pseudonim autora wariantu, bez znaku @.', 'tak', 'tekst'),
				field(
					'prepMinutes',
					'Czas przygotowania nadpisany w wariancie.',
					'nie',
					'liczba całkowita 0–2880 albo null (bez zmiany)'
				),
				field(
					'cookMinutes',
					'Czas gotowania nadpisany w wariancie.',
					'nie',
					'liczba całkowita 0–2880 albo null (bez zmiany)'
				),
				field(
					'servingsOverride',
					'Liczba porcji nadpisana w wariancie.',
					'nie',
					'liczba całkowita 1–100 albo null (bez zmiany)'
				),
				field('utensilsAdded', 'Naczynia dodane w wariancie.', 'tak', 'lista nazw, może być pusta'),
				field(
					'utensilsRemoved',
					'Naczynia usunięte w wariancie.',
					'tak',
					'lista nazw, może być pusta'
				),
				field(
					'ingredientOps',
					'Zmiany składników względem przepisu, tabela poniżej.',
					'tak',
					'lista, może być pusta'
				),
				field(
					'stepOps',
					'Zmiany kroków względem przepisu, tabela poniżej.',
					'tak',
					'lista, może być pusta'
				)
			]
		},
		{
			id: 'object-ops',
			heading: 'Zmiany wariantu (ingredientOps, stepOps)',
			rows: [
				field(
					'op: modify',
					'Zmiana składnika: targetKey, productKey, quantity, note, optional.',
					'tak, dla zmiany składnika',
					'targetKey wskazuje składnik przepisu'
				),
				field(
					'op: remove',
					'Usunięcie składnika lub kroku: targetKey (dla kroku także section).',
					'tak, dla usunięcia',
					'targetKey wskazuje wiersz przepisu'
				),
				field(
					'op: add',
					'Dodanie składnika lub kroku: key, afterKey; dla składnika productKey, quantity, note, optional; dla kroku section i blocks.',
					'tak, dla dodania',
					'afterKey: klucz wiersza przepisu lub wcześniejszego dodania; null na początku'
				),
				field(
					'op: replace',
					'Zmiana treści kroku: section, targetKey, blocks.',
					'tak, dla zmiany kroku',
					'section: prep albo instructions'
				)
			]
		},
		{
			id: 'object-product',
			heading: 'Produkt (products)',
			rows: [
				field('key', 'Klucz produktu, unikalny w pliku.', 'tak', 'tekst'),
				field('name', 'Nazwa produktu.', 'tak', '2–80 znaków'),
				field(
					'category',
					'Kategoria produktu.',
					'tak',
					'vegetables, fruits, meat, fish, dairy, bakery, dryGoods, spices, oilsVinegars, sauces, canned, frozen, drinks, sweets, other'
				),
				field(
					'measureKind',
					'Rodzaj miary: masa, objętość lub sztuki.',
					'tak',
					'mass, volume, count'
				),
				field('displayClass', 'Sposób wyświetlania ilości.', 'tak', 'weight, liquid, spice, piece'),
				field(
					'hasSizes',
					'Czy produkt ma wielkości sztuk (mała, średnia, duża).',
					'tak',
					'true lub false'
				),
				field('allowHalf', 'Czy można podać połowę sztuki.', 'tak', 'true lub false'),
				field('gramsPerMl', 'Gęstość: gramy w mililitrze.', 'nie', 'liczba albo null'),
				field('gramsPerPiece', 'Masa średniej sztuki w gramach.', 'nie', 'liczba albo null'),
				field('defaultPackageAmount', 'Typowa wielkość opakowania.', 'nie', 'liczba albo null'),
				field(
					'pieceForms',
					'Formy odmiany nazwy sztuki.',
					'nie',
					'obiekt z polami one, few, many, fraction albo null'
				)
			]
		},
		{
			id: 'object-custom-unit',
			heading: 'Własna jednostka (customUnits)',
			rows: [
				field('key', 'Klucz jednostki, unikalny w pliku.', 'tak', 'tekst'),
				field('name', 'Nazwa jednostki w mianowniku, np. „puszka”.', 'tak', '1–40 znaków'),
				field('forms', 'Formy odmiany: one, few, many, fraction.', 'tak', 'cztery teksty'),
				field(
					'productKey',
					'Produkt, do którego należy jednostka.',
					'nie',
					'brak lub null: jednostka dla każdego produktu'
				),
				field('measureKind', 'Rodzaj miary jednostki.', 'tak', 'mass, volume, count'),
				field(
					'amount',
					'Ile bazowej jednostki (g, ml lub sztuk) ma jedna własna jednostka.',
					'tak',
					'liczba większa od zera'
				)
			]
		},
		{
			id: 'object-image',
			heading: 'Zdjęcie (images)',
			rows: [
				field(
					'klucz zdjęcia',
					'Klucz, pod którym zdjęcie jest w images, np. img1.',
					'tak',
					'tekst'
				),
				field('contentType', 'Typ zawartości.', 'tak', 'image/webp'),
				field('dataBase64', 'Zawartość pliku zapisana w base64.', 'tak', 'tekst base64')
			]
		}
	] satisfies FormatTable[],
	units: {
		heading: 'Jednostki',
		intro:
			'Wartości pola unit i ich znaczenie. Wielkości sztuk w polu size: small (mała, 0,75 × średnia), medium (średnia), large (duża, 1,3 × średnia).',
		columns: { value: 'Wartość', name: 'Jednostka', conversion: 'Przeliczenie' },
		rows: [
			{ value: 'g', name: 'gram', conversion: '—' },
			{ value: 'kg', name: 'kilogram', conversion: '1000 g' },
			{ value: 'ml', name: 'mililitr', conversion: '—' },
			{ value: 'l', name: 'litr', conversion: '1000 ml' },
			{ value: 'glass', name: 'szklanka', conversion: '250 ml' },
			{ value: 'tbsp', name: 'łyżka', conversion: '15 ml' },
			{ value: 'tsp', name: 'łyżeczka', conversion: '5 ml' },
			{ value: 'drop', name: 'kropla', conversion: '0,05 ml' },
			{ value: 'pinch', name: 'szczypta', conversion: '0,3 ml' },
			{ value: 'piece', name: 'sztuka', conversion: 'sztuka średnia' },
			{ value: 'custom', name: 'własna jednostka', conversion: 'wymaga customUnitKey' }
		]
	},
	matching: {
		heading: 'Dopasowanie przy imporcie',
		items: [
			'Produkty, tagi, naczynia i jednostki własne są dopasowywane po nazwie. Wielkość liter, spacje na początku, na końcu i w środku oraz polskie znaki diakrytyczne (np. ą, ł) nie mają znaczenia.',
			'Produkt, tag lub naczynie, którego nie ma, zostaje utworzone z właściwościami z pliku.',
			'Własna jednostka jest użyta ponownie, gdy ma tę samą nazwę, ten sam zakres (ten sam produkt albo każdy produkt), ten sam rodzaj miary i ilość różniącą się najwyżej o 1 %. W przeciwnym razie powstaje nowa, a przy kolizji nazwy dostaje dopisek (2), (3).',
			'Import tworzy nowy, prywatny przepis należący do ciebie, razem z wariantami. Opisy dostają dopisek „Zaimportowano — oryginalnie: @autor”.'
		],
		duplicate: {
			heading: 'Gdy przepis o tym tytule już masz',
			text: 'Import pyta, co zrobić. „Dodaj jako wariant” dodaje plik jako warianty wybranego przepisu: przepis z pliku staje się wariantem „Wersja @autor”, a każdy wariant z pliku kolejnym wariantem. Różnice są liczone względem wybranego przepisu, pozycje identyczne są pomijane, a wynik podaje ich liczbę. „Importuj jako nowy przepis” działa jak zwykły import.'
		}
	},
	example: {
		heading: 'Przykład',
		intro:
			'Poniżej poprawny, kompletny plik: jeden przepis z dwoma składnikami, jednym krokiem i jednym wariantem, bez zdjęć.',
		label: 'Przykładowy plik JSON'
	}
};
