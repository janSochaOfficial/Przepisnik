import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers, flour } from './mocks/catalog';
import { mockApi, type MockHandlers } from './mocks/index';
import { detail, newRecipeId, recipeId, recipesHandlers } from './mocks/recipes';
import { can, cup, sauceHandlers, tomatoes, unitsHandlers } from './mocks/units';

// Custom units (PROJECT §5.6, D-31): creating one in the editor, and scaling in it in the recipe view.

const base: MockHandlers = {
	...signedIn,
	...catalogHandlers,
	...recipesHandlers,
	'GET /products': { body: [tomatoes, flour] },
	...unitsHandlers()
};

const ingredients = (page: Page) => page.getByRole('list', { name: 'Lista składników' });

test.describe('custom units in the recipe editor', () => {
	test('creates "puszka" for the product and saves "2 puszki"', async ({ page }) => {
		const api = await mockApi(page, {
			...base,
			'POST /recipes': { status: 201, body: detail({ id: newRecipeId }) }
		});
		await page.goto('/przepisy/nowy');
		await page.getByLabel('Tytuł').fill('Sos pomidorowy');

		const row = page.getByRole('listitem', { name: 'Składnik 1' });
		await row.getByLabel('Produkt').fill('pomid');
		await page.getByRole('option', { name: /pomidory krojone/ }).click();
		await row.getByLabel('Jednostka').click();
		// Built-in units for a mass product without density, its unit, a unit for every product
		// that fits (none: filiżanka needs a density), then the new-unit entry.
		await expect(page.getByRole('option')).toHaveText(['g', 'kg', /puszka/, '+ Nowa jednostka…']);
		await page.getByRole('option', { name: '+ Nowa jednostka…' }).click();

		const dialog = page.getByRole('dialog', { name: 'Nowa jednostka' });
		await dialog.getByLabel('Nazwa').fill('puszka');
		await expect(dialog.getByTestId('unit-example')).toHaveText(
			'1 puszka · 2 puszki · 5 puszek · ½ puszki'
		);
		await expect(
			dialog.getByRole('switch', { name: /Tylko dla produktu pomidory krojone/ })
		).toBeChecked();
		await dialog.getByLabel('Ilość').fill('400');
		await dialog.getByRole('button', { name: 'Dodaj jednostkę' }).click();
		await expect(dialog).toBeHidden();
		await expect(row.getByLabel('Jednostka')).toHaveText(/puszka/);

		await row.getByLabel('Ilość').fill('2');
		await page
			.getByRole('region', { name: 'Wykonanie' })
			.getByRole('listitem', { name: 'Krok 1' })
			.getByLabel('Tekst kroku')
			.fill('Gotuj.');
		await page.getByRole('button', { name: 'Zapisz przepis' }).click();
		await expect(page).toHaveURL(new RegExp(`/przepisy/${newRecipeId}$`));

		expect(api.calls.find((c) => c.key === 'POST /units')!.json).toEqual({
			name: 'puszka',
			forms: { one: 'puszka', few: 'puszki', many: 'puszek', fraction: 'puszki' },
			productId: tomatoes.id,
			equals: { value: 400, unit: 'g' }
		});
		const post = api.calls.find((c) => c.key === 'POST /recipes')!.json as {
			ingredients: unknown[];
		};
		expect(post.ingredients[0]).toEqual({
			productId: tomatoes.id,
			quantity: { value: 2, unit: 'custom', customUnitId: can.id },
			optional: false
		});
	});

	test('does not offer a volume unit for a mass product without density', async ({ page }) => {
		await mockApi(page, base);
		await page.goto('/przepisy/nowy');
		const row = page.getByRole('listitem', { name: 'Składnik 1' });
		await row.getByLabel('Produkt').fill('mąka');
		await page.getByRole('option', { name: /mąka pszenna/ }).click();
		await row.getByLabel('Jednostka').click();
		await expect(page.getByRole('option')).toHaveText(['g', 'kg', '+ Nowa jednostka…']);
		await expect(page.getByRole('option', { name: cup.name })).toHaveCount(0);
	});
});

test.describe('custom units in the recipe view', () => {
	test('shows "2 puszki" and keeps scaled amounts in the unit', async ({ page }) => {
		await mockApi(page, { ...base, ...sauceHandlers });
		await page.goto(`/przepisy/${recipeId}`);
		await expect(
			ingredients(page).getByRole('button', { name: /^2 puszki pomidory krojone/ })
		).toBeVisible();
		await expect(
			ingredients(page).getByRole('button', { name: /^1 filiżanka mleko/ })
		).toBeVisible();

		// "Mam: 1,5 puszki" → ×0,75.
		await ingredients(page)
			.getByRole('button', { name: /pomidory krojone/ })
			.click();
		const sheet = page.getByRole('dialog');
		await expect(sheet.getByLabel('Jednostka')).toHaveText(/puszka/);
		await sheet.getByLabel('Mam').fill('1,5');
		await sheet.getByRole('button', { name: 'Przelicz' }).click();
		await expect(
			ingredients(page).getByRole('button', { name: /^1 ½ puszki pomidory krojone/ })
		).toBeVisible();
		await expect(
			ingredients(page).getByRole('button', { name: /^¾ filiżanki mleko/ })
		).toBeVisible();

		// "Mam: 480 g" → ×0,6: 1,2 puszki shows as 1 ¼ (4 % off, no "~").
		await ingredients(page)
			.getByRole('button', { name: /pomidory krojone/ })
			.click();
		await page.getByRole('dialog').getByLabel('Jednostka').click();
		await page.getByRole('option', { name: 'g', exact: true }).click();
		await page.getByRole('dialog').getByLabel('Mam').fill('480');
		await page.getByRole('dialog').getByRole('button', { name: 'Przelicz' }).click();
		await expect(
			ingredients(page).getByRole('button', { name: /^1 ¼ puszki pomidory krojone/ })
		).toBeVisible();
	});
});
