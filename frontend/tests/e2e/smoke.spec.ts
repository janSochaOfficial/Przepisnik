// E2E smoke (T90): two users against the real stack. The steps build on each other, so they run serially.
import { expect, test, type BrowserContext, type Page } from '@playwright/test';
import { readFileSync } from 'node:fs';
import {
	choose,
	chooseVisibility,
	devLogin,
	pickProduct,
	recipeIdFrom,
	runId,
	solidPng
} from './helpers';

const ania = `ania_${runId}`;
const bartek = `bartek_${runId}`;
const publicTitle = `Placki na mleku ${runId}`;
const privateTitle = `Kisiel domowy ${runId}`;
const variantName = 'Więcej mąki';
const sharedFridge = `Lodówka Bartka ${runId}`;

test.describe.configure({ mode: 'serial' });

let contextA: BrowserContext;
let contextB: BrowserContext;
let a: Page;
let b: Page;
let publicId = '';
let sharedFridgeId = '';
let exportPath = '';

const ingredientList = (page: Page) => page.getByRole('list', { name: 'Lista składników' });

test.beforeAll(async ({ browser }) => {
	contextA = await browser.newContext();
	contextB = await browser.newContext();
	await contextB.grantPermissions(['clipboard-read', 'clipboard-write']);
	a = await contextA.newPage();
	b = await contextB.newPage();
	await devLogin(a, ania);
	await devLogin(b, bartek);
});

test.afterAll(async () => {
	await contextA?.close();
	await contextB?.close();
});

test('A creates a public recipe with a photo', async () => {
	await a.goto('/przepisy/nowy');
	await expect(a.getByRole('heading', { level: 1, name: 'Nowy przepis' })).toBeVisible();
	await a.getByLabel('Tytuł').fill(publicTitle);
	await a.getByLabel('Czas gotowania').fill('20');

	const flour = await pickProduct(a, 1, 'mąka pszenna');
	await flour.getByLabel('Ilość').fill('200');
	await choose(a, flour, 'Jednostka', 'g');

	await a.getByRole('button', { name: 'Dodaj składnik' }).click();
	const milk = await pickProduct(a, 2, 'mleko');
	await milk.getByLabel('Ilość').fill('500');
	await choose(a, milk, 'Jednostka', 'ml');

	await a.getByRole('button', { name: 'Dodaj składnik' }).click();
	const onion = await pickProduct(a, 3, 'cebula');
	await onion.getByLabel('Ilość').fill('1');

	const [chooser] = await Promise.all([
		a.waitForEvent('filechooser'),
		a.getByRole('button', { name: 'Dodaj zdjęcie' }).click()
	]);
	await chooser.setFiles({
		name: 'placki.png',
		mimeType: 'image/png',
		buffer: solidPng(320, 240, [200, 120, 40])
	});
	await expect(a.getByRole('img', { name: 'Zdjęcie główne przepisu' })).toBeVisible();

	const instructions = a.getByRole('region', { name: 'Wykonanie' });
	await instructions
		.getByRole('listitem', { name: 'Krok 1' })
		.getByLabel('Tekst kroku')
		.fill('Wymieszaj wszystko i smaż na złoto.');

	await chooseVisibility(a, a.locator('form'), 'Publiczny');
	await a.getByRole('button', { name: 'Zapisz przepis' }).click();

	await expect(a).toHaveURL(/\/przepisy\/[0-9a-f-]{36}$/);
	publicId = recipeIdFrom(a.url());
	await expect(a.getByRole('heading', { level: 1, name: publicTitle })).toBeVisible();
	const cover = a.getByRole('img', { name: 'Zdjęcie przepisu' });
	await expect(cover).toBeVisible();
	expect(await cover.evaluate((img: HTMLImageElement) => img.naturalWidth)).toBeGreaterThan(0);
});

test('B adds a public variant and favourites the recipe', async () => {
	await b.goto(`/przepisy/${publicId}`);
	await expect(b.getByRole('heading', { level: 1, name: publicTitle })).toBeVisible();

	await b.getByRole('button', { name: 'Dodaj do ulubionych' }).first().click();
	await expect(b.getByRole('button', { name: 'Usuń z ulubionych' }).first()).toBeVisible();

	await b.getByRole('button', { name: 'Więcej działań' }).click();
	await b
		.getByRole('dialog', { name: 'Działania' })
		.getByRole('button', { name: /wariant/i })
		.click();
	await expect(b).toHaveURL(new RegExp(`/przepisy/${publicId}/warianty/nowy$`));
	await b.getByLabel('Nazwa wariantu').fill(variantName);
	await b.getByRole('listitem', { name: 'Składnik 1' }).getByLabel('Ilość').fill('300');
	await chooseVisibility(b, b.locator('form'), 'Publiczny');
	await b.getByRole('button', { name: 'Zapisz wariant' }).click();
	await expect(b).toHaveURL(new RegExp(`/przepisy/${publicId}\\?wariant=`));

	await b.goto('/przepisy?tab=mine&sub=favorites');
	await expect(b.getByRole('link', { name: new RegExp(publicTitle) }).first()).toBeVisible();
});

test('A can no longer edit the recipe or narrow its visibility', async () => {
	await a.goto(`/przepisy/${publicId}/edytuj`);
	await expect(
		a.getByRole('heading', { name: 'Publicznego przepisu nie można edytować' })
	).toBeVisible();
	await expect(a.getByLabel('Tytuł')).toHaveCount(0);

	await a.getByRole('button', { name: 'Zmień widoczność' }).click();
	const dialog = a.getByRole('dialog', { name: 'Zmień widoczność' });
	await dialog.getByRole('radio', { name: /Prywatny/ }).check({ force: true });
	await dialog.getByRole('button', { name: 'Zmień widoczność' }).click();
	await expect(
		dialog.getByText(
			'Nie można zawęzić widoczności — inni użytkownicy mają warianty tego przepisu.'
		)
	).toBeVisible();
	await expect(dialog.getByText('Warianty innych osób: 1.')).toBeVisible();
});

test('A edits a private recipe freely, then confirms deleting its variants', async () => {
	await a.goto('/przepisy/nowy');
	await a.getByLabel('Tytuł').fill(privateTitle);
	const milk = await pickProduct(a, 1, 'mleko');
	await milk.getByLabel('Ilość').fill('1000');
	await choose(a, milk, 'Jednostka', 'ml');
	await a
		.getByRole('region', { name: 'Wykonanie' })
		.getByRole('listitem', { name: 'Krok 1' })
		.getByLabel('Tekst kroku')
		.fill('Zagotuj mleko.');
	await a.getByRole('button', { name: 'Zapisz przepis' }).click();
	await expect(a).toHaveURL(/\/przepisy\/[0-9a-f-]{36}$/);
	const privateId = recipeIdFrom(a.url());

	// 1. A free edit: no variants yet, so no question.
	await a.goto(`/przepisy/${privateId}/edytuj`);
	await a.getByLabel('Tytuł').fill(`${privateTitle} (1)`);
	await a.getByRole('button', { name: 'Zapisz przepis' }).click();
	await expect(a).toHaveURL(new RegExp(`/przepisy/${privateId}$`));
	await expect(a.getByRole('heading', { level: 1, name: `${privateTitle} (1)` })).toBeVisible();

	// 2. A variant of it.
	await a.goto(`/przepisy/${privateId}/warianty/nowy`);
	await a.getByLabel('Nazwa wariantu').fill('Pół litra');
	await a.getByRole('listitem', { name: 'Składnik 1' }).getByLabel('Ilość').fill('500');
	await a.getByRole('button', { name: 'Zapisz wariant' }).click();
	await expect(a).toHaveURL(new RegExp(`/przepisy/${privateId}\\?wariant=`));

	// 3. Editing again warns up front and asks before deleting the variant.
	await a.goto(`/przepisy/${privateId}/edytuj`);
	await expect(
		a.getByText('Zapisanie zmian usunie wszystkie warianty tego przepisu (1).')
	).toBeVisible();
	await a.getByLabel('Tytuł').fill(`${privateTitle} (2)`);
	await a.getByRole('button', { name: 'Zapisz przepis' }).click();
	const confirm = a.getByRole('alertdialog', { name: 'Usunąć warianty?' });
	await confirm.getByRole('button', { name: 'Zapisz i usuń warianty' }).click();
	await expect(a).toHaveURL(new RegExp(`/przepisy/${privateId}$`));
	await expect(a.getByRole('heading', { level: 1, name: `${privateTitle} (2)` })).toBeVisible();

	await a.goto('/przepisy?tab=variants');
	await expect(a.getByText('Pół litra')).toHaveCount(0);
});

test('B shares a fridge with A', async () => {
	await b.goto('/lodowka');
	await expect(b).toHaveURL(/\/lodowka\/[0-9a-f-]{36}$/);
	sharedFridgeId = /\/lodowka\/([0-9a-f-]{36})/.exec(b.url())![1]!;

	await b.getByRole('button', { name: 'Ustawienia lodówki' }).click();
	const settings = b.getByRole('dialog', { name: 'Ustawienia lodówki' });
	await settings.getByLabel('Nazwa lodówki').fill(sharedFridge);
	await settings.getByRole('button', { name: 'Zmień nazwę' }).click();
	await expect(b.getByText('Zmieniono nazwę.')).toBeVisible();
	await settings.getByRole('button', { name: 'Utwórz zaproszenie' }).click();
	await expect(b.getByText('Skopiowano link zaproszenia.')).toBeVisible();
	const link = await b.evaluate(() => navigator.clipboard.readText());
	expect(link).toMatch(/\/zaproszenie\/[A-Za-z0-9_-]+$/);
	await b.keyboard.press('Escape');

	await b.getByRole('button', { name: 'Dodaj produkt' }).click();
	const add = b.getByRole('dialog', { name: 'Dodaj do lodówki' });
	await add.getByLabel('Produkt').fill('mleko');
	await b
		.getByRole('option', { name: /^mleko/ })
		.first()
		.click();
	await add.getByLabel('Opakowanie').fill('1000');
	await add.getByRole('button', { name: 'Dodaj do lodówki' }).click();
	await expect(b.getByRole('button', { name: 'mleko' })).toBeVisible();

	await a.goto(new URL(link).pathname);
	await a.getByRole('button', { name: 'Dołącz' }).click();
	await expect(a.getByText('Dołączono do lodówki.')).toBeVisible();
});

test("A picks B's variant, scales and deducts from the shared fridge; the shopping list gets the rest", async () => {
	await a.goto(`/przepisy/${publicId}`);
	await expect(a.getByText('Przepis podstawowy')).toBeVisible();

	// Pick B's option for the flour slot: the variant becomes active.
	await ingredientList(a)
		.getByRole('button', { name: /mąka pszenna/ })
		.click();
	await a.getByRole('dialog').getByRole('button', { name: /300 g/ }).click();
	await expect(a.getByText(variantName, { exact: true })).toBeVisible();
	await expect(
		ingredientList(a).getByRole('button', { name: /^300 g mąka pszenna/ })
	).toBeVisible();

	// "Mam 600 g" doubles everything.
	await ingredientList(a)
		.getByRole('button', { name: /mąka pszenna/ })
		.click();
	const sheet = a.getByRole('dialog');
	await sheet.getByLabel('Mam').fill('600');
	await sheet.getByRole('button', { name: 'Przelicz' }).click();
	await expect(a.getByRole('status').filter({ hasText: 'Przeliczono' })).toContainText(
		'Przeliczono ×2'
	);
	if (await sheet.isVisible()) await a.keyboard.press('Escape');

	await a.getByRole('button', { name: 'Odejmij z lodówki' }).click();
	const dialog = a.getByRole('dialog', { name: 'Odejmij z lodówki' });
	await choose(a, dialog, 'Lodówka', sharedFridge);
	await expect(dialog.getByText('Brak w lodówce').first()).toBeVisible();
	await dialog.getByRole('button', { name: 'Dodaj brakujące do listy zakupów' }).click();
	await expect(a.getByText(/Dodano do listy zakupów: 2\./)).toBeVisible();
	await dialog.getByRole('button', { name: 'Odejmij zaznaczone' }).click();
	await expect(a.getByRole('status').filter({ hasText: 'Odjęto 1 produkt' })).toBeVisible();

	// The milk package lost 1000 ml × … of its 1000 ml: it is gone or reduced; B sees the change.
	await b.goto(`/lodowka/${sharedFridgeId}`);
	await expect(b.getByRole('button', { name: 'mleko' })).toHaveCount(0);

	await b.goto(`/lista-zakupow?fridge=${sharedFridgeId}`);
	await expect(b.getByRole('checkbox', { name: 'Kupione: mąka pszenna' })).toBeVisible();
	await expect(b.getByRole('checkbox', { name: 'Kupione: cebula' })).toBeVisible();
});

test('A exports the recipe and B imports it', async () => {
	await a.goto(`/przepisy/${publicId}`);
	await a.getByRole('button', { name: 'Więcej działań' }).click();
	const [download] = await Promise.all([
		a.waitForEvent('download'),
		a.getByRole('dialog', { name: 'Działania' }).getByRole('button', { name: 'Eksportuj' }).click()
	]);
	exportPath = test.info().outputPath('export.json');
	await download.saveAs(exportPath);
	const exported = JSON.parse(readFileSync(exportPath, 'utf8')) as {
		format: string;
		recipe: { title: string; coverImageKey: string | null };
		variants: { name: string }[];
	};
	expect(exported.format).toBe('recipes-list/recipe');
	expect(exported.recipe.title).toBe(publicTitle);
	expect(exported.recipe.coverImageKey).not.toBeNull();
	expect(exported.variants.map((v) => v.name)).toEqual([variantName]);

	await b.goto('/przepisy?import=1');
	const dialog = b.getByRole('dialog', { name: 'Importuj przepis' });
	await dialog.getByLabel('Plik z przepisem').setInputFiles(exportPath);
	await dialog.getByRole('button', { name: 'Importuj' }).click();
	await expect(b).toHaveURL(/\/przepisy\/[0-9a-f-]{36}$/);
	expect(recipeIdFrom(b.url())).not.toBe(publicId);
	await expect(b.getByRole('heading', { level: 1, name: publicTitle })).toBeVisible();
	await expect(b.getByText(`oryginalnie: @${ania}`)).toBeVisible();
});
