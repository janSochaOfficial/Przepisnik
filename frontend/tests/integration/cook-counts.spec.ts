import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { deductionHandlers, fridgeHandlers, fridgeId, item } from './mocks/fridge';
import { mockApi, problem, type MockHandlers } from './mocks/index';
import { recipeId, recipesHandlers } from './mocks/recipes';
import { baseRecipe, chicken, variantsHandlers } from './mocks/variants';

const url = `/przepisy/${recipeId}`;
const HOUR = 60 * 60 * 1000;
const chickenItem = item('00000000-0000-7000-8000-0000000d0a01', chicken, 1000, 100);

function handlers(extra: MockHandlers = {}, lastCountedAt: string | null = null): MockHandlers {
	return {
		...signedIn,
		...catalogHandlers,
		...recipesHandlers,
		...variantsHandlers,
		'GET /recipes/:id': { body: baseRecipe({ myLastCountedCookAt: lastCountedAt }) },
		...fridgeHandlers([chickenItem]),
		...deductionHandlers(),
		...extra
	};
}

const hoursAgo = (h: number) => new Date(Date.now() - h * HOUR).toISOString();
const actionBar = (page: Page) => page.getByRole('group', { name: 'Działania na przepisie' });
const cookedButton = (page: Page) => actionBar(page).getByRole('button', { name: 'Ugotowane!' });
const variantsTab = (page: Page) => page.getByRole('tab', { name: /Warianty/ });

/** The mock for "Ugotowane!": answers like the server and remembers the call. */
function cookedHandler(): MockHandlers {
	return {
		'POST /recipes/:id/cooked': () => ({
			body: {
				counted: true,
				recipeCookCount: 6,
				nextAllowedAt: new Date(Date.now() + 12 * HOUR).toISOString()
			}
		})
	};
}

test.describe('"Ugotowane!"', () => {
	test('records a cook for the base, confirms with a toast and blocks the button for 12 hours', async ({
		page
	}) => {
		const api = await mockApi(page, handlers(cookedHandler()));
		await page.goto(url);
		await expect(cookedButton(page)).toBeEnabled();

		await cookedButton(page).click();

		await expect(page.getByText('Zapisano: ugotowane!')).toBeVisible();
		expect(api.calls.find((c) => c.key === `POST /recipes/${recipeId}/cooked`)!.json).toEqual({
			variantId: null
		});
		await expect(cookedButton(page)).toBeDisabled();
		await expect(
			page.getByText(/^Zapisano jako ugotowane\. Ponownie od (\d{1,2} \p{L}+, )?\d{2}:\d{2}$/u)
		).toBeVisible();

		// The Warianty tab shows the new count of the base without a reload.
		await variantsTab(page).click();
		await expect(page.getByText('ugotowano: 6')).toBeVisible();
	});

	test('is disabled with the hint (and the date) when the last counted cook is recent', async ({
		page
	}) => {
		await mockApi(page, handlers({}, hoursAgo(1)));
		await page.goto(url);
		await expect(cookedButton(page)).toBeDisabled();
		await expect(page.getByText(/^Zapisano jako ugotowane\. Ponownie od /)).toBeVisible();
	});

	test('is enabled again once 12 hours have passed', async ({ page }) => {
		await mockApi(page, handlers({}, hoursAgo(12.5)));
		await page.goto(url);
		await expect(cookedButton(page)).toBeEnabled();
		await expect(page.getByText(/Zapisano jako ugotowane/)).toHaveCount(0);
	});

	test('a 409 from the server shows the same hint', async ({ page }) => {
		await mockApi(
			page,
			handlers({
				'POST /recipes/:id/cooked': problem(409, 'cook.too_soon', {
					nextAllowedAt: new Date(Date.now() + 3 * HOUR).toISOString()
				})
			})
		);
		await page.goto(url);

		await cookedButton(page).click();

		await expect(cookedButton(page)).toBeDisabled();
		await expect(page.getByText(/^Zapisano jako ugotowane\. Ponownie od /)).toBeVisible();
	});

	test('sends the shown variant', async ({ page }) => {
		const api = await mockApi(page, handlers(cookedHandler()));
		await page.goto(url);
		await variantsTab(page).click();
		await page.getByRole('button', { name: /^Kremowa/ }).click();

		await cookedButton(page).click();

		await expect(page.getByText('Zapisano: ugotowane!')).toBeVisible();
		const body = api.calls.find((c) => c.key === `POST /recipes/${recipeId}/cooked`)!.json as {
			variantId: string;
		};
		expect(body.variantId).toBe('00000000-0000-7000-8000-0000000b0b01');
	});
});

test.describe('the deduction and the 12-hour rule', () => {
	const open = async (page: Page) => {
		await page.getByRole('button', { name: 'Odejmij z lodówki' }).click();
		return page.getByRole('dialog', { name: 'Odejmij z lodówki' });
	};
	const warning =
		'Ten przepis był już liczony jako ugotowany w ciągu ostatnich 12 godzin, więc licznik się nie zwiększy.';

	test('warns within 12 hours of a counted cook and still deducts', async ({ page }) => {
		const api = await mockApi(page, handlers(deductionHandlers(undefined, false), hoursAgo(2)));
		await page.goto(url);
		const dialog = await open(page);
		await expect(dialog.getByText(warning)).toBeVisible();

		await dialog.getByRole('button', { name: 'Odejmij zaznaczone' }).click();

		await expect(page.getByText('Odjęto 1 produkt')).toBeVisible();
		expect(api.calls.some((c) => c.key === `POST /fridges/${fridgeId}/deductions`)).toBe(true);
	});

	test('no warning without a recent counted cook', async ({ page }) => {
		await mockApi(page, handlers());
		await page.goto(url);
		const dialog = await open(page);
		await expect(dialog.getByRole('switch', { name: 'kurczak' })).toBeVisible();
		await expect(dialog.getByText(warning)).toHaveCount(0);
	});

	test('a counted deduction disables "Ugotowane!" and bumps the count', async ({ page }) => {
		await mockApi(page, handlers());
		await page.goto(url);
		const dialog = await open(page);
		await dialog.getByRole('button', { name: 'Odejmij zaznaczone' }).click();
		await expect(page.getByText('Odjęto 1 produkt')).toBeVisible();

		await expect(cookedButton(page)).toBeDisabled();
		await variantsTab(page).click();
		await expect(page.getByText('ugotowano: 6')).toBeVisible();
	});

	test('a deduction the server did not count leaves the button alone', async ({ page }) => {
		await mockApi(page, handlers(deductionHandlers(undefined, false)));
		await page.goto(url);
		const dialog = await open(page);
		await dialog.getByRole('button', { name: 'Odejmij zaznaczone' }).click();
		await expect(page.getByText('Odjęto 1 produkt')).toBeVisible();

		await expect(cookedButton(page)).toBeEnabled();
	});
});

test.describe('accessibility', () => {
	for (const colorScheme of ['light', 'dark'] as const) {
		test(`the action bar with the hint and the dialog with the warning pass axe (${colorScheme})`, async ({
			page
		}) => {
			await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
			await mockApi(page, handlers({}, hoursAgo(2)));
			await page.goto(url);
			await expect(cookedButton(page)).toBeDisabled();
			expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);

			await page.getByRole('button', { name: 'Odejmij z lodówki' }).click();
			const dialog = page.getByRole('dialog', { name: 'Odejmij z lodówki' });
			await expect(dialog.getByText(/więc licznik się nie zwiększy/)).toBeVisible();
			await expect(dialog.getByRole('switch', { name: 'kurczak' })).toBeVisible();
			expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
		});
	}

	test('the action bar fits a 360 px phone', async ({ page }) => {
		await page.setViewportSize({ width: 360, height: 740 });
		await mockApi(page, handlers());
		await page.goto(url);
		await expect(cookedButton(page)).toBeVisible();
		const bar = await actionBar(page).boundingBox();
		for (const name of ['Dodaj brakujące do listy', 'Ugotowane!', 'Odejmij z lodówki']) {
			const box = await actionBar(page).getByRole('button', { name }).boundingBox();
			expect(box!.x).toBeGreaterThanOrEqual(bar!.x);
			expect(box!.x + box!.width).toBeLessThanOrEqual(bar!.x + bar!.width + 1);
		}
	});
});
