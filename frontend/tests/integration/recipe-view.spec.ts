import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers, uploadedImage } from './mocks/catalog';
import { mockApi, problem, type MockHandlers } from './mocks/index';
import { ania, recipeId, recipesHandlers } from './mocks/recipes';
import {
	baseIngredientIds,
	baseRecipe,
	cream,
	matrix,
	resolved,
	selectionMemory,
	tofu,
	variantA,
	variantB,
	variantDetail,
	variantsHandlers
} from './mocks/variants';

const base: MockHandlers = {
	...signedIn,
	...catalogHandlers,
	...recipesHandlers,
	...variantsHandlers
};
const url = `/przepisy/${recipeId}`;

const ingredients = (page: Page) => page.getByRole('list', { name: 'Lista składników' });

/** Opens the option sheet of an ingredient row and picks an option. */
async function pickOption(page: Page, row: RegExp, option: RegExp) {
	await ingredients(page).getByRole('button', { name: row }).click();
	await page.getByRole('dialog').getByRole('button', { name: option }).click();
}

test.describe('auto-match', () => {
	test('shows the base, and picking a variant option switches the other slots and the steps', async ({
		page
	}) => {
		await mockApi(page, base);
		await page.goto(url);
		await expect(page.getByRole('heading', { level: 1, name: 'Gulasz z kurczaka' })).toBeVisible();
		await expect(page.getByText('Przepis podstawowy')).toBeVisible();
		await expect(page.getByText('Krok bazowy: podsmaż kurczaka.')).toBeVisible();
		await expect(
			ingredients(page).getByRole('button', { name: /1 łyżeczka kardamon/ })
		).toBeVisible();

		await pickOption(page, /kurczak/, /tofu/);

		// Variant A: tofu, and the kardamon amount follows A (2 łyżeczki), steps are A's.
		await expect(ingredients(page).getByRole('button', { name: /tofu/ })).toBeVisible();
		await expect(
			ingredients(page).getByRole('button', { name: /2 łyżeczki kardamon/ })
		).toBeVisible();
		await expect(page.getByText('Wersja wege', { exact: true })).toBeVisible();
		await expect(page.getByText('Krok wariantu A: podsmaż tofu.')).toBeVisible();
		await expect(page.getByText('Twój wybór')).toBeVisible();
	});

	test('the added slot follows its afterSlotKey', async ({ page }) => {
		await mockApi(page, base);
		await page.goto(`${url}?wariant=${'00000000-0000-7000-8000-0000000b0c01'}`);
		await expect(ingredients(page).getByRole('button')).toHaveCount(5);
		const names = await ingredients(page).getByRole('button').allInnerTexts();
		expect(names.map((n) => n.replace(/\s+/g, ' '))).toEqual([
			expect.stringContaining('kurczak'),
			expect.stringContaining('papryka'),
			expect.stringContaining('mleko'),
			expect.stringContaining('cebula'),
			expect.stringContaining('kardamon')
		]);
	});

	test('pins from two variants make a custom mix, and "Utwórz nowy wariant" prefills the editor', async ({
		page
	}) => {
		const api = await mockApi(page, {
			...base,
			'POST /recipes/:id/variants': {
				status: 201,
				body: variantDetail({ id: '00000000-0000-7000-8000-0000000b0d01', name: 'Mój wariant' })
			}
		});
		await page.goto(url);
		await pickOption(page, /kurczak/, /tofu/);
		await pickOption(page, /mleko/, /śmietanka/);
		await expect(page.getByText('Własna kombinacja', { exact: true })).toBeVisible();

		await ingredients(page)
			.getByRole('button', { name: /śmietanka/ })
			.click();
		await page
			.getByRole('dialog')
			.getByRole('button', { name: '+ Utwórz nowy wariant z bieżącego wyboru' })
			.click();

		await expect(page).toHaveURL(/\/warianty\/nowy$/);
		await expect(page.getByRole('heading', { level: 1, name: 'Nowy wariant' })).toBeVisible();
		await expect(page.getByLabel('Produkt').first()).toHaveValue('tofu');

		await page.getByLabel('Nazwa wariantu').fill('Mój wariant');
		await page.getByRole('button', { name: 'Zapisz wariant' }).click();
		await expect(page).toHaveURL(/\?wariant=00000000-0000-7000-8000-0000000b0d01$/);

		const post = api.calls.find((c) => c.key === `POST /recipes/${recipeId}/variants`);
		const body = post?.json as {
			name: string;
			visibility: string;
			recipe: { ingredients: { id?: string; productId: string; quantity?: unknown }[] };
		};
		expect(body.name).toBe('Mój wariant');
		expect(body.visibility).toBe('private');
		expect(body.recipe.ingredients.map((i) => [i.id, i.productId])).toEqual([
			[baseIngredientIds.chicken, tofu.id],
			[baseIngredientIds.milk, cream.id],
			[baseIngredientIds.onion, expect.any(String)],
			[baseIngredientIds.cardamom, expect.any(String)]
		]);
		expect(body.recipe.ingredients[3]?.quantity).toEqual({ value: 2, unit: 'tsp' });
	});

	test('"Wyczyść wybór" removes the pins', async ({ page }) => {
		await mockApi(page, base);
		await page.goto(url);
		await pickOption(page, /kurczak/, /tofu/);
		await page.getByRole('button', { name: 'Wyczyść wybór' }).click();
		await expect(ingredients(page).getByRole('button', { name: /kurczak/ })).toBeVisible();
		await expect(page.getByRole('button', { name: 'Wyczyść wybór' })).toBeHidden();
	});

	test('?wariant= pins every slot to that variant', async ({ page }) => {
		await mockApi(page, base);
		await page.goto(`${url}?wariant=${variantB}`);
		await expect(ingredients(page).getByRole('button', { name: /śmietanka/ })).toBeVisible();
		await expect(page.getByText('Krok wariantu B: dodaj śmietankę.')).toBeVisible();
	});

	test('a favourite variant becomes the auto choice once the pins are cleared', async ({
		page
	}) => {
		let favourite = false;
		const api = await mockApi(page, {
			...base,
			'GET /recipes/:id/variant-matrix': () => ({ body: matrix(favourite ? [variantA] : []) }),
			'PUT /variants/:id/favorite': () => {
				favourite = true;
				return { status: 204 };
			}
		});
		await page.goto(`${url}?wariant=${variantA}`);
		await page.getByRole('button', { name: 'Dodaj do ulubionych' }).last().click();
		await expect(page.getByRole('button', { name: 'Usuń z ulubionych' })).toHaveCount(1);
		expect(api.calls.some((c) => c.key === `PUT /variants/${variantA}/favorite`)).toBe(true);

		await page.getByRole('button', { name: 'Wyczyść wybór' }).click();
		// Without pins the base would win on cook count, but the ♥ ranks higher.
		await expect(page.getByText('Wersja wege', { exact: true })).toBeVisible();
	});
});

test.describe('header', () => {
	test('shows the cover photo when the recipe has one', async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /recipes/:id': { body: baseRecipe({ coverImage: uploadedImage }) }
		});
		await page.goto(url);
		await expect(page.getByRole('img', { name: 'Zdjęcie przepisu' })).toHaveAttribute(
			'src',
			uploadedImage.url
		);
	});
});

test.describe('scaling', () => {
	test('"Mam 600 g" on a 400 g anchor multiplies everything by 1,5 and can be reset', async ({
		page
	}) => {
		await mockApi(page, base);
		await page.goto(url);
		await ingredients(page)
			.getByRole('button', { name: /kurczak/ })
			.click();
		const sheet = page.getByRole('dialog');
		await sheet.getByLabel('Mam').fill('600');
		await sheet.getByRole('button', { name: 'Przelicz' }).click();

		await expect(page.getByRole('status').filter({ hasText: 'Przeliczono' })).toContainText(
			'Przeliczono ×1,5 według: kurczak 600 g'
		);
		await expect(ingredients(page).getByRole('button', { name: /^600 g kurczak/ })).toBeVisible();
		// liquid in glasses, a spice in spoons, onions as a size combination
		await expect(
			ingredients(page).getByRole('button', { name: /1 ½ szklanki mleko/ })
		).toBeVisible();
		await expect(
			ingredients(page).getByRole('button', { name: /1 ½ łyżeczki kardamon/ })
		).toBeVisible();
		await expect(ingredients(page).getByRole('button', { name: /cebula/ })).toContainText('3 duże');

		await page
			.getByRole('status')
			.filter({ hasText: 'Przeliczono' })
			.getByRole('button', { name: 'Przywróć' })
			.click();
		await expect(ingredients(page).getByRole('button', { name: /^400 g kurczak/ })).toBeVisible();
		await expect(
			ingredients(page).getByRole('button', { name: /^1 szklanka mleko/ })
		).toBeVisible();
		await expect(ingredients(page).getByRole('button', { name: /2 duże cebula/ })).toBeVisible();
	});
});

test.describe('selection memory', () => {
	test('saves once after rapid picks (debounced) and restores picks and scaling on reload', async ({
		page
	}) => {
		const memory = selectionMemory();
		const api = await mockApi(page, { ...base, ...memory.handlers });
		await page.goto(url);
		await pickOption(page, /kurczak/, /tofu/);
		await pickOption(page, /tofu/, /kurczak/);
		await pickOption(page, /kurczak/, /tofu/);
		await ingredients(page).getByRole('button', { name: /tofu/ }).click();
		await page.getByRole('dialog').getByLabel('Mam').fill('600');
		await page.getByRole('dialog').getByRole('button', { name: 'Przelicz' }).click();

		await expect
			.poll(
				() => api.calls.filter((c) => c.key === `PUT /recipes/${recipeId}/my-selection`).length,
				{
					timeout: 5000
				}
			)
			.toBe(1);
		await page.waitForTimeout(1500);
		expect(
			api.calls.filter((c) => c.key.startsWith('PUT') && c.key.endsWith('my-selection'))
		).toHaveLength(1);

		await page.reload();
		await expect(ingredients(page).getByRole('button', { name: /tofu/ })).toBeVisible();
		await expect(page.getByRole('status').filter({ hasText: 'Przeliczono' })).toContainText(
			'Przeliczono ×1,5'
		);
	});
});

test.describe('menu', () => {
	test('the author of a public recipe sees "Utwórz wariant" instead of "Edytuj"', async ({
		page
	}) => {
		await mockApi(page, {
			...base,
			'GET /recipes/:id': {
				body: baseRecipe({ visibility: 'public', canEdit: false, author: ania })
			}
		});
		await page.goto(url);
		await page.getByRole('button', { name: 'Więcej działań' }).click();
		const menu = page.getByRole('dialog', { name: 'Działania' });
		await expect(menu.getByRole('link', { name: 'Edytuj' })).toBeHidden();
		await expect(menu.getByRole('button', { name: 'Utwórz wariant' })).toBeVisible();
		await expect(menu.getByRole('button', { name: 'Zmień widoczność' })).toBeVisible();
		await expect(menu.getByRole('button', { name: 'Eksportuj' })).toBeVisible();
	});

	test('an archived recipe offers no editing actions (D-27)', async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /recipes/:id': { body: baseRecipe({ isArchived: true, canEdit: false }) }
		});
		await page.goto(url);
		await page.getByRole('button', { name: 'Więcej działań' }).click();
		const menu = page.getByRole('dialog', { name: 'Działania' });
		await expect(menu.getByRole('link', { name: 'Edytuj' })).toBeHidden();
		await expect(menu.getByRole('button', { name: /wariant/i })).toBeHidden();
		await expect(menu.getByRole('button', { name: 'Zmień widoczność' })).toBeHidden();
		await expect(menu.getByRole('button', { name: 'Eksportuj' })).toBeVisible();
	});
});

test.describe('variant editor', () => {
	const editHandlers: MockHandlers = {
		...base,
		'GET /variants/:id': { body: variantDetail() },
		'GET /variants/:id/resolved': ({ params }) => ({ body: resolved[params.id ?? ''] })
	};

	test('edits a variant: base rows are locked, removed ones come back, the PUT carries the version', async ({
		page
	}) => {
		const api = await mockApi(page, {
			...editHandlers,
			'PUT /variants/:id': { body: variantDetail({ version: 4 }) }
		});
		await page.goto(`/warianty/${variantA}/edytuj`);
		await expect(page.getByRole('heading', { level: 1, name: 'Edytuj wariant' })).toBeVisible();
		await expect(page.getByLabel('Nazwa wariantu')).toHaveValue('Wersja wege');
		await page.getByLabel('Nazwa wariantu').fill('Wege 2');
		await page.getByRole('button', { name: 'Zapisz wariant' }).click();
		const put = api.calls.find((c) => c.key === `PUT /variants/${variantA}`);
		const body = put?.json as { name: string; version: number; recipe: { ingredients: unknown[] } };
		expect(body.name).toBe('Wege 2');
		expect(body.version).toBe(3);
		expect(body.recipe.ingredients).toHaveLength(4);
	});

	test('a version conflict reloads the version and asks to save again', async ({ page }) => {
		let version = 3;
		const api = await mockApi(page, {
			...editHandlers,
			'GET /variants/:id': () => ({ body: variantDetail({ version }) }),
			'PUT /variants/:id': ({ json }) => {
				const sent = (json as { version: number }).version;
				if (sent === version) return { body: variantDetail({ version: version + 1 }) };
				return problem(409, 'concurrency.conflict');
			}
		});
		await page.goto(`/warianty/${variantA}/edytuj`);
		await expect(page.getByLabel('Nazwa wariantu')).toHaveValue('Wersja wege');
		version = 4; // someone cooked the variant meanwhile
		await page.getByRole('button', { name: 'Zapisz wariant' }).click();
		await expect(page.getByText(/Wariant zmienił się w międzyczasie/)).toBeVisible();
		await page.getByRole('button', { name: 'Zapisz wariant' }).click();
		await expect(page).toHaveURL(/\/przepisy\/.*\?wariant=/);
		expect(api.calls.filter((c) => c.key.startsWith('PUT /variants/'))).toHaveLength(2);
	});

	test('shows the hint when the base recipe is narrower than the variant', async ({ page }) => {
		await mockApi(page, base);
		await page.goto(`${url}/warianty/nowy`);
		await page.getByRole('radio', { name: /Moje lodówki/ }).check({ force: true });
		await expect(
			page.getByText('Wariant zobaczą tylko osoby, które widzą przepis bazowy.')
		).toBeVisible();
	});
});

test.describe('accessibility', () => {
	test('the recipe view and its option sheet pass axe', async ({ page }) => {
		await mockApi(page, base);
		await page.goto(url);
		await expect(page.getByRole('heading', { level: 1 })).toBeVisible();
		expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
		await ingredients(page)
			.getByRole('button', { name: /kurczak/ })
			.click();
		await expect(page.getByRole('dialog')).toBeVisible();
		expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
	});

	test('on a phone the sheet sits at the bottom edge', async ({ page, isMobile }) => {
		test.skip(!isMobile, 'phone only');
		await mockApi(page, base);
		await page.goto(url);
		await ingredients(page)
			.getByRole('button', { name: /kurczak/ })
			.click();
		const box = await page.getByRole('dialog').boundingBox();
		const viewport = page.viewportSize();
		expect(box && viewport && box.y + box.height >= viewport.height - 1).toBe(true);
	});
});
