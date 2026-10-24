import { expect, test } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { mockApi, type MockHandlers } from './mocks/index';
import { recipesHandlers } from './mocks/recipes';

const base: MockHandlers = { ...signedIn, ...catalogHandlers, ...recipesHandlers };

test.describe('the "add to the catalog?" prompt', () => {
	test('"Dodaj do bazy" opens the new-product form with the typed name', async ({ page }) => {
		await mockApi(page, base);
		await page.goto('/przepisy/nowy');
		const field = page.getByRole('listitem', { name: 'Składnik 1' }).getByLabel('Produkt');

		await field.fill('kozieradka');
		await field.press('Tab');

		const prompt = page.getByRole('alertdialog');
		await expect(prompt).toContainText('„kozieradka” nie ma w bazie produktów. Dodać?');
		await prompt.getByRole('button', { name: 'Dodaj do bazy' }).click();

		const dialog = page.getByRole('dialog', { name: 'Dodaj produkt' });
		await expect(dialog.getByLabel('Nazwa produktu')).toHaveValue('kozieradka');
	});

	test('"Nie dodawaj" empties the field and returns focus to it', async ({ page }) => {
		await mockApi(page, base);
		await page.goto('/przepisy/nowy');
		const field = page.getByRole('listitem', { name: 'Składnik 1' }).getByLabel('Produkt');

		await field.fill('kozieradka');
		await field.press('Tab');
		await page.getByRole('alertdialog').getByRole('button', { name: 'Nie dodawaj' }).click();

		await expect(page.getByRole('alertdialog')).toBeHidden();
		await expect(field).toHaveValue('');
		await expect(field).toBeFocused();
	});

	test('a name that matches a product selects it without asking', async ({ page }) => {
		await mockApi(page, base);
		await page.goto('/przepisy/nowy');
		const row = page.getByRole('listitem', { name: 'Składnik 1' });
		const field = row.getByLabel('Produkt');

		await field.fill('Cebula');
		await field.press('Tab');

		await expect(field).toHaveValue('cebula');
		await expect(page.getByRole('alertdialog')).toBeHidden();
	});
});
