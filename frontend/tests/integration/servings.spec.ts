import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { mockApi, type MockHandlers } from './mocks/index';
import { recipeId, recipesHandlers } from './mocks/recipes';
import { servingsEditorHandlers, servingsHandlers } from './mocks/servings';
import type { Schemas } from './mocks/types';
import { variantsHandlers } from './mocks/variants';

// Servings with scaling (R2, PROJECT §5.7, D-37).
const base: MockHandlers = { ...signedIn, ...catalogHandlers, ...recipesHandlers };
const view: MockHandlers = { ...base, ...variantsHandlers, ...servingsHandlers };
const url = `/przepisy/${recipeId}`;

const ingredients = (page: Page) => page.getByRole('list', { name: 'Lista składników' });
const servingsField = (page: Page) => page.getByRole('textbox', { name: 'Porcje:' });
const banner = (page: Page) => page.getByRole('status').filter({ hasText: 'Przeliczono' });

test('the editor sends "Liczba porcji"', async ({ page }) => {
	const api = await mockApi(page, { ...base, ...servingsEditorHandlers });
	await page.goto(`/przepisy/${recipeId}/edytuj`);
	await expect(page.getByLabel('Tytuł')).toHaveValue('Placki ziemniaczane');
	await page.getByLabel('Liczba porcji').fill('4');
	await page.getByRole('button', { name: 'Zapisz przepis' }).click();
	await expect(page).toHaveURL(new RegExp(`/przepisy/${recipeId}$`));

	const put = api.calls.find((c) => c.key === `PUT /recipes/${recipeId}`);
	expect((put?.json as Schemas['RecipeUpdate']).servings).toBe(4);
});

test('the editor field refuses servings outside 1–100', async ({ page }) => {
	await mockApi(page, { ...base, ...servingsEditorHandlers });
	await page.goto(`/przepisy/${recipeId}/edytuj`);
	const field = page.getByLabel('Liczba porcji');
	await field.fill('101');
	await field.blur();
	await expect(page.getByText('Największa wartość to 100.')).toBeVisible();
	await field.fill('0');
	await field.blur();
	await expect(page.getByText('Najmniejsza wartość to 1.')).toBeVisible();
});

test('changing 4 servings to 6 scales the amounts ×1,5, shows the banner and resets', async ({
	page
}) => {
	const api = await mockApi(page, view);
	await page.goto(url);
	await expect(servingsField(page)).toHaveValue('4');
	await expect(ingredients(page).getByRole('button', { name: /^400 g kurczak/ })).toBeVisible();

	await page.getByRole('button', { name: 'Więcej porcji' }).click();
	await page.getByRole('button', { name: 'Więcej porcji' }).click();

	await expect(servingsField(page)).toHaveValue('6');
	await expect(banner(page)).toContainText('Przeliczono ×1,5 według: 6 porcji');
	await expect(ingredients(page).getByRole('button', { name: /^600 g kurczak/ })).toBeVisible();
	await expect(ingredients(page).getByRole('button', { name: /1 ½ szklanki mleko/ })).toBeVisible();

	await expect
		.poll(
			() => api.calls.filter((c) => c.key === `PUT /recipes/${recipeId}/my-selection`).at(-1)?.json
		)
		.toMatchObject({ scaling: { kind: 'servings', servings: 6 } });

	await banner(page).getByRole('button', { name: 'Przywróć' }).click();
	await expect(servingsField(page)).toHaveValue('4');
	await expect(ingredients(page).getByRole('button', { name: /^400 g kurczak/ })).toBeVisible();
	await expect(banner(page)).toBeHidden();
});

test('typing a number of servings scales too; an invalid one is refused', async ({ page }) => {
	await mockApi(page, view);
	await page.goto(url);
	await servingsField(page).fill('2');
	await servingsField(page).press('Enter');
	await expect(banner(page)).toContainText('Przeliczono ×0,5 według: 2 porcje');
	await expect(ingredients(page).getByRole('button', { name: /^200 g kurczak/ })).toBeVisible();

	await servingsField(page).fill('0');
	await servingsField(page).press('Enter');
	await expect(page.getByText('Podaj liczbę porcji od 1 do 100.')).toBeVisible();
	await expect(banner(page)).toContainText('2 porcje');
});

test('"Mam:" on an ingredient replaces the servings choice and shows servings × factor', async ({
	page
}) => {
	await mockApi(page, view);
	await page.goto(url);
	await page.getByRole('button', { name: 'Więcej porcji' }).click();
	await ingredients(page)
		.getByRole('button', { name: /kurczak/ })
		.click();
	const sheet = page.getByRole('dialog');
	await sheet.getByLabel('Mam').fill('300');
	await sheet.getByRole('button', { name: 'Przelicz' }).click();

	await expect(banner(page)).toContainText('Przeliczono ×0,75 według: kurczak 300 g');
	await expect(servingsField(page)).toHaveValue('3');
	await expect(page.getByText('Przeliczone składniki wystarczą na 3 porcje.')).toBeVisible();
});

test('a recipe without servings shows no servings control', async ({ page }) => {
	await mockApi(page, { ...base, ...variantsHandlers });
	await page.goto(url);
	await expect(page.getByRole('heading', { level: 1, name: 'Gulasz z kurczaka' })).toBeVisible();
	await expect(ingredients(page).getByRole('button', { name: /kurczak/ })).toBeVisible();
	await expect(page.getByText('Porcje:')).toHaveCount(0);
});

for (const colorScheme of ['light', 'dark'] as const) {
	test(`the servings control and its banner pass axe (${colorScheme})`, async ({ page }) => {
		await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
		await mockApi(page, view);
		await page.goto(url);
		await expect(servingsField(page)).toBeVisible();
		await page.getByRole('button', { name: 'Więcej porcji' }).click();
		await expect(banner(page)).toBeVisible();
		expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
	});
}
