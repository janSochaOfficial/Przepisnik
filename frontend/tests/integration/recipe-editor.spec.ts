import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { cardamom, catalogHandlers, flour, onion, tinyPng, uploadedImage } from './mocks/catalog';
import { mockApi, problem, type MockHandlers } from './mocks/index';
import { detail, newRecipeId, recipeId, recipesHandlers, sampleExport } from './mocks/recipes';
import type { Schemas } from './mocks/types';

const base: MockHandlers = { ...signedIn, ...catalogHandlers, ...recipesHandlers };

async function pickProduct(page: Page, row: number, query: string, option: RegExp) {
	const group = page.getByRole('listitem', { name: `Składnik ${row}` });
	await group.getByLabel('Produkt').fill(query);
	await page.getByRole('option', { name: option }).click();
	return group;
}

test.describe('creating a recipe', () => {
	test('posts the contract shape with a new product and a photo inside a step', async ({
		page
	}) => {
		const api = await mockApi(page, {
			...base,
			'POST /recipes': { status: 201, body: detail({ id: newRecipeId }) }
		});
		await page.goto('/przepisy/nowy');
		await expect(page.getByRole('heading', { level: 1, name: 'Nowy przepis' })).toBeVisible();

		await page.getByLabel('Tytuł').fill('Gulasz wołowy');
		await page.getByLabel('Czas przygotowania').fill('10');
		await page.getByLabel('Czas gotowania').fill('30');
		await page.getByLabel('Tagi').fill('obiad');
		await page.getByLabel('Tagi').press('Enter');

		// 1. flour: mass product without density, so only g and kg are offered.
		const first = await pickProduct(page, 1, 'mąka', /mąka pszenna/);
		await first.getByLabel('Ilość').fill('200');
		await first.getByLabel('Jednostka').click();
		await expect(page.getByRole('option')).toHaveText(['g', 'kg', '+ Nowa jednostka…']);
		await page.getByRole('option', { name: 'g', exact: true }).click();

		// 2. onion: countable with sizes.
		await page.getByRole('button', { name: 'Dodaj składnik' }).click();
		const second = await pickProduct(page, 2, 'ceb', /cebula/);
		await second.getByLabel('Ilość').fill('2');
		await second.getByLabel('Wielkość').click();
		await page.getByRole('option', { name: 'duża' }).click();

		// 3. a product that does not exist yet, created inline.
		await page.getByRole('button', { name: 'Dodaj składnik' }).click();
		const third = page.getByRole('listitem', { name: 'Składnik 3' });
		await third.getByLabel('Produkt').fill('kardamon');
		await page.getByRole('option', { name: /Dodaj „kardamon”/ }).click();
		const dialog = page.getByRole('dialog', { name: 'Dodaj produkt' });
		await dialog.getByLabel('Jak mierzymy produkt').click();
		await page.getByRole('option', { name: 'Objętość (mililitry)' }).click();
		await dialog.getByLabel('Jak pokazywać ilość').click();
		await page.getByRole('option', { name: 'Przyprawa' }).click();
		await dialog.getByRole('button', { name: 'Dodaj produkt' }).click();
		await expect(dialog).toBeHidden();
		await expect(third.getByLabel('Produkt')).toHaveValue('kardamon');
		await third.getByLabel('Ilość').fill('1');
		await third.getByLabel('Jednostka').click();
		await page.getByRole('option', { name: 'łyżeczka' }).click();

		// Instruction step 1: a photo goes between the two paragraphs.
		const instructions = page.getByRole('region', { name: 'Wykonanie' });
		const step1 = instructions.getByRole('listitem', { name: 'Krok 1' });
		const text = step1.getByLabel('Tekst kroku');
		await text.fill('Pierwszy akapit.\nDrugi akapit.');
		await text.evaluate((el: HTMLTextAreaElement) => {
			el.focus();
			el.setSelectionRange(16, 16);
		});
		const [chooser] = await Promise.all([
			page.waitForEvent('filechooser'),
			step1.getByRole('button', { name: 'Wstaw zdjęcie' }).click()
		]);
		await chooser.setFiles({ name: 'gulasz.png', mimeType: 'image/png', buffer: tinyPng });
		await expect(step1.getByRole('img')).toBeVisible();
		await expect(step1.getByLabel('Tekst kroku')).toHaveCount(2);
		await expect(step1.getByLabel('Tekst kroku').first()).toHaveValue('Pierwszy akapit.');
		await expect(step1.getByLabel('Tekst kroku').last()).toHaveValue('Drugi akapit.');

		// Instruction step 2.
		await instructions.getByRole('button', { name: 'Dodaj krok' }).click();
		await instructions
			.getByRole('listitem', { name: 'Krok 2' })
			.getByLabel('Tekst kroku')
			.fill('Podawaj z kaszą.');

		await page.getByRole('button', { name: 'Zapisz przepis' }).click();
		await expect(page).toHaveURL(new RegExp(`/przepisy/${newRecipeId}$`));

		const post = api.calls.find((c) => c.key === 'POST /recipes');
		const expected = {
			title: 'Gulasz wołowy',
			prepMinutes: 10,
			cookMinutes: 30,
			tagIds: ['00000000-0000-7000-8000-00000000b0ff'],
			utensilIds: [],
			ingredients: [
				{ productId: flour.id, quantity: { value: 200, unit: 'g' }, optional: false },
				{
					productId: onion.id,
					quantity: { value: 2, unit: 'piece', size: 'large' },
					optional: false
				},
				{ productId: cardamom.id, quantity: { value: 1, unit: 'tsp' }, optional: false }
			],
			prepSteps: [],
			instructionSteps: [
				{
					blocks: [
						{ type: 'text', text: 'Pierwszy akapit.' },
						{ type: 'image', imageId: uploadedImage.id },
						{ type: 'text', text: 'Drugi akapit.' }
					]
				},
				{ blocks: [{ type: 'text', text: 'Podawaj z kaszą.' }] }
			],
			visibility: 'private'
		} satisfies Schemas['RecipeCreate'];
		expect(post?.json).toEqual(expected);
		expect(api.calls.map((c) => c.key)).toContain('POST /products');
	});

	test('shows client validation without calling the API', async ({ page }) => {
		const api = await mockApi(page, base);
		await page.goto('/przepisy/nowy');
		await page.getByRole('button', { name: 'Zapisz przepis' }).click();

		await expect(page.getByText('To pole jest wymagane.')).toBeVisible();
		await expect(page.getByText('Wybierz produkt.')).toBeVisible();
		expect(api.calls.some((c) => c.key === 'POST /recipes')).toBe(false);
	});

	test('maps a server unit_incompatible error onto the right row', async ({ page }) => {
		await mockApi(page, {
			...base,
			'PUT /recipes/:id': () =>
				problem(400, 'quantity.unit_incompatible', {
					errors: { 'ingredients[1].quantity': ['quantity.unit_incompatible'] }
				})
		});
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await page.getByLabel('Tytuł').fill('Inna nazwa');
		await page.getByRole('button', { name: 'Zapisz przepis' }).click();

		const message = 'Tej jednostki nie można użyć dla tego produktu.';
		await expect(
			page.getByRole('listitem', { name: 'Składnik 2' }).getByText(message)
		).toBeVisible();
		await expect(page.getByRole('listitem', { name: 'Składnik 1' }).getByText(message)).toHaveCount(
			0
		);
	});

	test('asks before making a recipe public', async ({ page }) => {
		await mockApi(page, base);
		await page.goto('/przepisy/nowy');
		const publicRadio = page.getByRole('radio', { name: /Publiczny/ });
		const privateRadio = page.getByRole('radio', { name: /Prywatny/ });
		await expect(privateRadio).toBeChecked();

		await publicRadio.click();
		const confirm = page.getByRole('alertdialog', { name: 'Zrobić przepis publicznym?' });
		await expect(
			confirm.getByText('Publicznego przepisu nie można edytować', { exact: false })
		).toBeVisible();
		await confirm.getByRole('button', { name: 'Anuluj' }).click();
		await expect(privateRadio).toBeChecked();

		await publicRadio.click();
		await confirm.getByRole('button', { name: 'Zrób publicznym' }).click();
		await expect(publicRadio).toBeChecked();
	});

	test('warns before leaving with unsaved changes', async ({ page }) => {
		await mockApi(page, base);
		await page.goto('/przepisy/nowy');
		await page.getByLabel('Tytuł').fill('Nie zapisuj mnie');
		await page.getByRole('link', { name: 'Anuluj' }).click();

		const leave = page.getByRole('alertdialog', { name: 'Porzucić zmiany?' });
		await expect(leave).toBeVisible();
		await leave.getByRole('button', { name: 'Anuluj' }).click();
		await expect(page).toHaveURL(/\/przepisy\/nowy$/);

		await page.getByRole('link', { name: 'Anuluj' }).click();
		await leave.getByRole('button', { name: 'Porzuć zmiany' }).click();
		await expect(page).toHaveURL(/\/przepisy$/);
	});
});

test.describe('editing a recipe', () => {
	test('keeps ingredient ids and sends the version', async ({ page }) => {
		const loaded = detail();
		const api = await mockApi(page, {
			...base,
			'PUT /recipes/:id': { body: detail({ title: 'Nowa nazwa' }) }
		});
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await expect(page.getByLabel('Tytuł')).toHaveValue('Placki ziemniaczane');
		await page.getByLabel('Tytuł').fill('Nowa nazwa');
		await page.getByRole('button', { name: 'Zapisz przepis' }).click();
		await expect(page).toHaveURL(new RegExp(`/przepisy/${recipeId}$`));

		const put = api.calls.find((c) => c.key === `PUT /recipes/${recipeId}`);
		const body = put?.json as Schemas['RecipeUpdate'];
		expect(body.ingredients.map((i) => i.id)).toEqual(loaded.ingredients.map((i) => i.id));
		expect(body.instructionSteps.map((s) => s.id)).toEqual(
			loaded.instructionSteps.map((s) => s.id)
		);
		expect(body.version).toBe(loaded.version);
		expect(body.deleteVariants).toBe(false);
		expect(body.title).toBe('Nowa nazwa');
	});

	const withVariants = detail({ totalVariantCount: 3, foreignVariantCount: 1, variantCount: 3 });
	const hasVariants: MockHandlers = {
		...base,
		'GET /recipes/:id': { body: withVariants },
		'PUT /recipes/:id': ({ json }) =>
			(json as Schemas['RecipeUpdate']).deleteVariants
				? { body: detail() }
				: problem(409, 'recipe.has_variants', { variantCount: 3, foreignVariantCount: 1 })
	};

	test('shows the variants warning up front', async ({ page }) => {
		await mockApi(page, hasVariants);
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await expect(
			page.getByText(
				'Zapisanie zmian usunie wszystkie warianty tego przepisu (3, w tym 1 innej osoby).'
			)
		).toBeVisible();
	});

	test('confirming a 409 has_variants retries with deleteVariants', async ({ page }) => {
		const api = await mockApi(page, hasVariants);
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await page.getByLabel('Tytuł').fill('Zmieniony');
		await page.getByRole('button', { name: 'Zapisz przepis' }).click();

		const dialog = page.getByRole('alertdialog', { name: 'Usunąć warianty?' });
		await expect(dialog).toContainText(
			'Zapisanie zmian usunie wszystkie warianty tego przepisu (3, w tym 1 innej osoby). Kontynuować?'
		);
		await dialog.getByRole('button', { name: 'Zapisz i usuń warianty' }).click();
		await expect(page).toHaveURL(new RegExp(`/przepisy/${recipeId}$`));

		const puts = api.calls.filter((c) => c.key === `PUT /recipes/${recipeId}`);
		expect(puts.map((c) => (c.json as Schemas['RecipeUpdate']).deleteVariants)).toEqual([
			false,
			true
		]);
	});

	test('cancelling keeps the unsaved form and sends nothing more', async ({ page }) => {
		const api = await mockApi(page, hasVariants);
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await page.getByLabel('Tytuł').fill('Zmieniony');
		await page.getByRole('button', { name: 'Zapisz przepis' }).click();

		const dialog = page.getByRole('alertdialog', { name: 'Usunąć warianty?' });
		await dialog.getByRole('button', { name: 'Anuluj' }).click();
		await expect(dialog).toBeHidden();

		expect(api.calls.filter((c) => c.key === `PUT /recipes/${recipeId}`)).toHaveLength(1);
		await expect(page.getByLabel('Tytuł')).toHaveValue('Zmieniony');
		// Still dirty: leaving asks first.
		await page.getByRole('link', { name: 'Anuluj' }).click();
		await expect(page.getByRole('alertdialog', { name: 'Porzucić zmiany?' })).toBeVisible();
	});

	test('a public recipe offers "Utwórz wariant" instead of the form', async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /recipes/:id': { body: detail({ visibility: 'public', canEdit: false }) }
		});
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await expect(
			page.getByRole('heading', { name: 'Publicznego przepisu nie można edytować' })
		).toBeVisible();
		await expect(page.getByRole('link', { name: 'Utwórz wariant' })).toHaveAttribute(
			'href',
			`/przepisy/${recipeId}/warianty/nowy`
		);
		await expect(page.getByLabel('Tytuł')).toHaveCount(0);
	});

	test('an archived recipe is read-only', async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /recipes/:id': { body: detail({ isArchived: true, canEdit: false }) }
		});
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await expect(
			page.getByRole('heading', { name: 'Ten przepis jest zarchiwizowany' })
		).toBeVisible();
		await expect(page.getByLabel('Tytuł')).toHaveCount(0);
		await expect(page.getByRole('button', { name: 'Zmień widoczność' })).toHaveCount(0);
	});

	test('narrowing visibility with a 409 explains why', async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /recipes/:id': { body: detail({ visibility: 'public', canEdit: false }) },
			'PUT /recipes/:id/visibility': () =>
				problem(409, 'recipe.visibility_locked', { foreignVariantCount: 2 })
		});
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await page.getByRole('button', { name: 'Zmień widoczność' }).click();
		const dialog = page.getByRole('dialog', { name: 'Zmień widoczność' });
		await dialog.getByRole('radio', { name: /Moje lodówki/ }).click();
		await dialog.getByRole('button', { name: 'Zmień widoczność' }).click();

		await expect(
			dialog.getByText(
				'Nie można zawęzić widoczności — inni użytkownicy mają warianty tego przepisu.'
			)
		).toBeVisible();
		await expect(dialog.getByText('Warianty innych osób: 2.')).toBeVisible();
	});

	test('deleting a recipe that others have variants of says it is hidden', async ({ page }) => {
		const api = await mockApi(page, {
			...base,
			'GET /recipes/:id': { body: detail({ totalVariantCount: 2, foreignVariantCount: 1 }) },
			'DELETE /recipes/:id': { status: 204 }
		});
		await page.goto(`/przepisy/${recipeId}/edytuj`);
		await page.getByRole('button', { name: 'Usuń przepis' }).click();
		const dialog = page.getByRole('alertdialog', { name: 'Usunąć przepis?' });
		await expect(dialog).toContainText('zostanie ukryty');
		await dialog.getByRole('button', { name: 'Usuń przepis' }).click();

		await expect(page).toHaveURL(/\/przepisy$/);
		await expect(
			page.getByText('Przepis zostanie ukryty, bo inni mają jego warianty.')
		).toBeVisible();
		expect(api.calls.map((c) => c.key)).toContain(`DELETE /recipes/${recipeId}`);
	});
});

test.describe('importing a recipe', () => {
	test('picks a file, posts it and opens the new recipe', async ({ page }) => {
		const api = await mockApi(page, {
			...base,
			'POST /recipes/import': {
				status: 201,
				body: detail({ id: newRecipeId, title: 'Zaimportowany przepis' })
			}
		});
		await page.goto('/przepisy?import=1');
		const dialog = page.getByRole('dialog', { name: 'Importuj przepis' });
		await expect(dialog).toBeVisible();
		await dialog.getByLabel('Plik z przepisem').setInputFiles({
			name: 'export.json',
			mimeType: 'application/json',
			buffer: Buffer.from(JSON.stringify(sampleExport))
		});
		await dialog.getByRole('button', { name: 'Importuj' }).click();

		await expect(page).toHaveURL(new RegExp(`/przepisy/${newRecipeId}$`));
		const post = api.calls.find((c) => c.key === 'POST /recipes/import');
		expect(post?.json).toEqual(sampleExport);
	});

	test('rejects a file that is not an export', async ({ page }) => {
		await mockApi(page, {
			...base,
			'POST /recipes/import': () => problem(400, 'import.invalid_format')
		});
		await page.goto('/przepisy?import=1');
		const dialog = page.getByRole('dialog', { name: 'Importuj przepis' });
		await dialog.getByLabel('Plik z przepisem').setInputFiles({
			name: 'export.json',
			mimeType: 'application/json',
			buffer: Buffer.from('{}')
		});
		await dialog.getByRole('button', { name: 'Importuj' }).click();
		await expect(
			dialog.getByText('Ten plik nie jest prawidłowym eksportem przepisu.')
		).toBeVisible();
	});
});

test.describe('accessibility', () => {
	for (const colorScheme of ['light', 'dark'] as const) {
		test(`the editor has no axe violations (${colorScheme})`, async ({ page }) => {
			await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
			await mockApi(page, base);
			await page.goto(`/przepisy/${recipeId}/edytuj`);
			await expect(page.getByLabel('Tytuł')).toBeVisible();
			await page.evaluate(() => document.fonts.ready);
			const result = await new AxeBuilder({ page })
				.withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
				.analyze();
			expect(
				result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' '))}`)
			).toEqual([]);
		});
	}
});
