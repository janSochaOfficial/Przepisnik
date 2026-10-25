import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { mockApi } from './mocks/index';
import { recipesHandlers, sampleExport } from './mocks/recipes';
import { transferHandlers } from './mocks/transfer';

// "Format pliku" help page for the import (PROJECT §4.7, D-40). The page is static; it only needs
// the signed-in session that the app shell asks for.
const FORMAT_URL = '/przepisy/import/format';
const EXAMPLE_TITLE = 'Jajecznica na mleku';

async function openFormatPage(page: Page) {
	await mockApi(page, { ...signedIn });
	await page.goto(FORMAT_URL);
	await expect(page.getByRole('heading', { level: 1, name: 'Format pliku importu' })).toBeVisible();
}

test('the import dialog links to the format page, which opens in a new tab', async ({
	page,
	context
}) => {
	await mockApi(page, {
		...transferHandlers([]),
		...signedIn,
		...catalogHandlers,
		...recipesHandlers
	});
	await page.goto('/przepisy?import=1');
	const dialog = page.getByRole('dialog', { name: 'Importuj przepis' });
	const link = dialog.getByRole('link', { name: 'Format pliku' });
	await expect(link).toHaveAttribute('target', '_blank');
	await expect(link).toHaveAttribute('rel', /noopener/);

	const [formatPage] = await Promise.all([context.waitForEvent('page'), link.click()]);
	await mockApi(formatPage, { ...signedIn });
	await formatPage.waitForLoadState();
	await expect(formatPage).toHaveURL(new RegExp(`${FORMAT_URL}$`));
	await expect(
		formatPage.getByRole('heading', { level: 1, name: 'Format pliku importu' })
	).toBeVisible();
	// The dialog keeps its state: it is still open on the first page.
	await expect(dialog).toBeVisible();
});

test('the page shows every section and the example', async ({ page }) => {
	await openFormatPage(page);

	for (const name of [
		'Co zawiera plik',
		'Budowa pliku',
		'Przepis (recipe)',
		'Składnik (ingredients)',
		'Wariant (variants)',
		'Jednostki',
		'Dopasowanie przy imporcie',
		'Przykład'
	]) {
		await expect(page.getByRole('heading', { name, exact: true })).toBeVisible();
	}

	const top = page.getByRole('table').first();
	await expect(top.getByRole('columnheader', { name: 'Pole' })).toBeVisible();
	await expect(top.getByRole('row', { name: /formatVersion/ })).toBeVisible();

	const example = page.getByRole('region', { name: 'Przykładowy plik JSON' });
	await expect(example).toBeVisible();
	await expect(example).toContainText(`"title": "${EXAMPLE_TITLE}"`);
});

test('"Kopiuj" copies the complete example to the clipboard', async ({ page, context }) => {
	await openFormatPage(page);
	await context.grantPermissions(['clipboard-read', 'clipboard-write'], {
		origin: new URL(page.url()).origin
	});
	await page.getByRole('button', { name: 'Kopiuj' }).click();

	await expect(page.getByText('Skopiowano przykład do schowka.')).toBeVisible();
	const copied = await page.evaluate(() => navigator.clipboard.readText());
	const parsed = JSON.parse(copied);
	expect(parsed.format).toBe(sampleExport.format);
	expect(parsed.recipe.title).toBe(EXAMPLE_TITLE);
	expect(parsed.recipe.ingredients).toHaveLength(2);
	expect(parsed.variants).toHaveLength(1);
});

test('the example is reachable by keyboard and the page itself does not scroll sideways', async ({
	page
}) => {
	await openFormatPage(page);
	const example = page.getByRole('region', { name: 'Przykładowy plik JSON' });
	await example.focus();
	await expect(example).toBeFocused();
	const pageFits = await page.evaluate(
		() => document.documentElement.scrollWidth <= window.innerWidth
	);
	expect(pageFits).toBe(true);
});

for (const colorScheme of ['light', 'dark'] as const) {
	test(`the format page has no axe violations (${colorScheme})`, async ({ page }) => {
		await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
		await openFormatPage(page);
		await page.evaluate(() => document.fonts.ready);
		const result = await new AxeBuilder({ page }).analyze();
		expect(result.violations).toEqual([]);
	});
}
