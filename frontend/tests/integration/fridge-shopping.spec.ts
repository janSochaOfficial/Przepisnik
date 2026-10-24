import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import {
	cream,
	creamItem,
	deductionHandlers,
	eggItem,
	fridgeHandlers,
	fridgeId
} from './mocks/fridge';
import { mockApi, problem, type MockHandlers } from './mocks/index';
import { shoppingHandlers } from './mocks/shopping';

const base = (): MockHandlers => ({ ...signedIn, ...catalogHandlers, ...fridgeHandlers() });

test.describe('the fridge', () => {
	test('lists items by category with the remaining amount', async ({ page }) => {
		await mockApi(page, base());
		await page.goto(`/lodowka/${fridgeId}`);
		await expect(page.getByRole('heading', { level: 1, name: 'Lodówka' })).toBeVisible();
		const row = page.getByRole('button', { name: 'smietanka' });
		await expect(row).toContainText('330 ml');
		await expect(row).toContainText('zostało 70 % (230 ml)');
		await expect(page.getByRole('heading', { name: 'nabiał' })).toBeVisible();
	});

	test('redirects /lodowka to the default fridge', async ({ page }) => {
		await mockApi(page, base());
		await page.goto('/lodowka');
		await expect(page).toHaveURL(new RegExp(`/lodowka/${fridgeId}$`));
	});

	test('searches within the fridge', async ({ page }) => {
		await mockApi(page, base());
		await page.goto(`/lodowka/${fridgeId}`);
		await page.getByRole('searchbox', { name: 'Szukaj w lodówce' }).fill('maka');
		await expect(page.getByRole('button', { name: 'smietanka' })).toHaveCount(0);
		await expect(page.getByRole('button', { name: 'mąka pszenna' })).toBeVisible();
	});

	test('adds, edits and deletes an item (with undo)', async ({ page }) => {
		const api = await mockApi(page, base());
		await page.goto(`/lodowka/${fridgeId}`);

		// Add: the package amount is prefilled from the product's default.
		await page.getByRole('button', { name: 'Dodaj produkt' }).click();
		const add = page.getByRole('dialog', { name: 'Dodaj do lodówki' });
		await add.getByLabel('Produkt').fill('mleko');
		await page.getByRole('option', { name: /mleko/ }).click();
		await add.getByLabel('Opakowanie').fill('1000');
		await add.getByRole('button', { name: 'Dodaj do lodówki' }).click();
		await expect(page.getByRole('button', { name: 'mleko' })).toBeVisible();
		expect(
			api.calls.find((c) => c.key.startsWith('POST /fridges/') && c.key.endsWith('/items'))!.json
		).toMatchObject({ packageAmount: 1000, remainingPercent: 100 });

		// Edit: 70 % → 65 % with the keyboard on the slider.
		await page.getByRole('button', { name: 'smietanka' }).click();
		const edit = page.getByRole('dialog', { name: 'smietanka' });
		const thumb = edit.getByRole('slider', { name: 'Ile zostało' });
		await thumb.focus();
		await page.keyboard.press('ArrowLeft');
		await edit.getByRole('button', { name: 'Zapisz zmiany' }).click();
		await expect(page.getByRole('button', { name: 'smietanka' })).toContainText('65 %');
		expect(
			api.calls.find((c) => c.key === `PUT /fridges/${fridgeId}/items/${creamItem.id}`)!.json
		).toMatchObject({ remainingPercent: 65, version: 1 });

		// Delete, then undo.
		await page.getByRole('button', { name: 'smietanka' }).click();
		await page
			.getByRole('dialog', { name: 'smietanka' })
			.getByRole('button', { name: 'Usuń z lodówki' })
			.click();
		await expect(page.getByRole('button', { name: 'smietanka' })).toHaveCount(0);
		await page
			.getByRole('status')
			.filter({ hasText: 'Usunięto produkt z lodówki.' })
			.getByRole('button', { name: 'Cofnij' })
			.click();
		await expect(page.getByRole('button', { name: 'smietanka' })).toBeVisible();
	});

	test('counts pieces for count products', async ({ page }) => {
		const api = await mockApi(page, {
			...signedIn,
			...catalogHandlers,
			...fridgeHandlers([eggItem])
		});
		await page.goto(`/lodowka/${fridgeId}`);
		await page.getByRole('button', { name: 'jajka' }).click();
		const edit = page.getByRole('dialog', { name: 'jajka' });
		await expect(edit.getByRole('status')).toHaveText('6 / 10');
		await edit.getByRole('button', { name: 'Odejmij sztukę' }).click();
		await edit.getByRole('button', { name: 'Odejmij sztukę' }).click();
		await edit.getByRole('button', { name: 'Zapisz zmiany' }).click();
		await expect(page.getByRole('button', { name: 'jajka' })).toContainText('40 %');
		expect(api.calls.find((c) => c.key.startsWith('PUT '))!.json).toMatchObject({
			remainingPercent: 40
		});
	});

	test('creates an invite and copies the link', async ({ page, context }) => {
		await context.grantPermissions(['clipboard-read', 'clipboard-write']);
		await mockApi(page, base());
		await page.goto(`/lodowka/${fridgeId}`);
		await page.getByRole('button', { name: 'Ustawienia lodówki' }).click();
		const settings = page.getByRole('dialog', { name: 'Ustawienia lodówki' });
		await settings.getByRole('button', { name: 'Utwórz zaproszenie' }).click();
		await expect(page.getByText('Skopiowano link zaproszenia.')).toBeVisible();
		const copied = await page.evaluate(() => navigator.clipboard.readText());
		expect(copied).toMatch(/\/zaproszenie\/tok123$/);
		await expect(settings.getByRole('button', { name: 'Cofnij zaproszenie' })).toBeVisible();
	});

	test('the fridge page has no axe violations at 360 px', async ({ page }) => {
		await page.setViewportSize({ width: 360, height: 740 });
		await page.emulateMedia({ reducedMotion: 'reduce' });
		await mockApi(page, base());
		await page.goto(`/lodowka/${fridgeId}`);
		await page.getByRole('button', { name: 'smietanka' }).click();
		const thumb = page.getByRole('slider', { name: 'Ile zostało' });
		await expect(thumb).toBeVisible();
		const box = (await thumb.boundingBox())!;
		expect(box.x).toBeGreaterThanOrEqual(0);
		expect(box.x + box.width).toBeLessThanOrEqual(360);
		await page.evaluate(() => document.fonts.ready);
		const result = await new AxeBuilder({ page })
			.withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
			.analyze();
		expect(
			result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' '))}`)
		).toEqual([]);
	});
});

test.describe('the deduction dialog', () => {
	const harness = '/lodowka/harness-odejmowanie';
	const withItems = (extra: MockHandlers = deductionHandlers()): MockHandlers => ({
		...signedIn,
		...fridgeHandlers([creamItem, eggItem]),
		...extra
	});

	test('warns, deducts the selected rows and undoes', async ({ page }) => {
		const api = await mockApi(page, withItems());
		await page.goto(harness);
		await page.getByRole('button', { name: 'Odejmij z lodówki' }).click();
		const dialog = page.getByRole('dialog', { name: 'Odejmij z lodówki' });
		await expect(dialog.getByText('Za mało — jest 230 ml')).toBeVisible();
		await expect(dialog.getByText('Brak w lodówce')).toBeVisible();
		await expect(dialog.getByRole('switch', { name: 'maka' })).toBeDisabled();
		await expect(dialog.getByRole('switch', { name: 'sol' })).toHaveCount(0);

		await dialog.getByRole('button', { name: 'Odejmij zaznaczone' }).click();
		const toast = page.getByRole('status').filter({ hasText: 'Odjęto 2 produkty' });
		await expect(toast).toBeVisible();
		expect(api.calls.find((c) => c.key === `POST /fridges/${fridgeId}/deductions`)!.json).toEqual({
			recipeId: '00000000-0000-7000-8000-0000000e0001',
			lines: [
				{ productId: cream.id, amount: 330 },
				{ productId: eggItem.product.id, amount: 2 }
			]
		});
		await toast.getByRole('button', { name: 'Cofnij' }).click();
		await expect(page.getByText('Cofnięto odjęcie.')).toBeVisible();
		expect(api.calls.map((c) => c.key)).toContain(
			`POST /fridges/${fridgeId}/deductions/00000000-0000-7000-8000-0000000cd001/undo`
		);
	});

	test('a switched-off row is not sent', async ({ page }) => {
		const api = await mockApi(page, withItems());
		await page.goto(harness);
		await page.getByRole('button', { name: 'Odejmij z lodówki' }).click();
		const dialog = page.getByRole('dialog', { name: 'Odejmij z lodówki' });
		await dialog.getByRole('switch', { name: 'smietanka' }).click();
		await dialog.getByRole('button', { name: 'Odejmij zaznaczone' }).click();
		await expect(page.getByText('Odjęto 1 produkt')).toBeVisible();
		const body = api.calls.find((c) => c.key.endsWith('/deductions'))!.json as {
			lines: unknown[];
		};
		expect(body.lines).toHaveLength(1);
	});

	test('explains a 409 on undo', async ({ page }) => {
		await mockApi(page, withItems(deductionHandlers(problem(409, 'deduction.items_changed'))));
		await page.goto(harness);
		await page.getByRole('button', { name: 'Odejmij z lodówki' }).click();
		await page
			.getByRole('dialog', { name: 'Odejmij z lodówki' })
			.getByRole('button', { name: 'Odejmij wszystko' })
			.click();
		await page.getByRole('status').getByRole('button', { name: 'Cofnij' }).click();
		await expect(
			page.getByText('Nie można cofnąć — produkty w lodówce zmieniły się w międzyczasie.')
		).toBeVisible();
	});

	for (const [answer, includesOptional] of [
		['Tylko wymagane', false],
		['Dodaj też opcjonalne', true]
	] as const) {
		test(`asks about missing optional ingredients: "${answer}"`, async ({ page }) => {
			const api = await mockApi(page, { ...withItems(), ...shoppingHandlers() });
			await page.goto(`${harness}?optional=1`);
			await page.getByRole('button', { name: 'Dodaj brakujące do listy zakupów' }).click();
			const ask = page.getByRole('alertdialog');
			await expect(ask).toContainText('Brakuje też składników opcjonalnych: rodzynki.');
			await ask.getByRole('button', { name: answer }).click();
			await expect(
				page.getByText(`Dodano do listy zakupów: ${includesOptional ? 3 : 2}.`)
			).toBeVisible();
			const call = api.calls.find(
				(c) => c.key === `POST /fridges/${fridgeId}/shopping-list/items`
			)!;
			const ids = (call.json as { items: { productId: string }[] }).items.map((i) => i.productId);
			expect(ids.includes('00000000-0000-7000-8000-00000000c005')).toBe(includesOptional);
		});
	}

	test('closing the optional question adds nothing', async ({ page }) => {
		const api = await mockApi(page, { ...withItems(), ...shoppingHandlers() });
		await page.goto(`${harness}?optional=1`);
		await page.getByRole('button', { name: 'Dodaj brakujące do listy zakupów' }).click();
		await expect(page.getByRole('alertdialog')).toBeVisible();
		await page.keyboard.press('Escape');
		await expect(page.getByRole('alertdialog')).toBeHidden();
		expect(api.calls.some((c) => c.key.startsWith('POST') && c.key.includes('shopping'))).toBe(
			false
		);
	});

	test('adds missing and short amounts to the shopping list', async ({ page }) => {
		const api = await mockApi(page, {
			...withItems(),
			...shoppingHandlers()
		});
		await page.goto(harness);
		await page.getByRole('button', { name: 'Dodaj brakujące do listy zakupów' }).click();
		await expect(page.getByText('Dodano do listy zakupów: 2.')).toBeVisible();
		const call = api.calls.find((c) => c.key === `POST /fridges/${fridgeId}/shopping-list/items`)!;
		expect(call.json).toEqual({
			items: [
				{ productId: cream.id, amount: 330 - 231 },
				{ productId: '00000000-0000-7000-8000-00000000c002', amount: 200 }
			]
		});
	});
});

test.describe('the shopping list', () => {
	const handlers = (): MockHandlers => ({ ...base(), ...shoppingHandlers() });

	test('shows unchecked items first and checks one off optimistically', async ({ page }) => {
		const api = await mockApi(page, handlers());
		await page.goto('/lista-zakupow');
		await expect(page.getByRole('heading', { level: 1, name: 'Lista zakupów' })).toBeVisible();
		await expect(page.getByRole('checkbox')).toHaveCount(3);
		await expect(page.getByRole('checkbox').first()).toHaveAccessibleName('Kupione: mleko');
		await expect(page.getByText('1,5 l')).toBeVisible();
		await page.getByRole('checkbox', { name: 'Kupione: mleko' }).check();
		await expect(page.getByRole('checkbox', { name: 'Kupione: mleko' })).toBeChecked();
		await expect.poll(() => api.calls.filter((c) => c.key.startsWith('PATCH')).length).toBe(1);
	});

	test('removes the bought items after confirming', async ({ page }) => {
		const api = await mockApi(page, handlers());
		await page.goto('/lista-zakupow');
		await page.getByRole('button', { name: 'Usuń kupione' }).click();
		await page.getByRole('alertdialog').getByRole('button', { name: 'Usuń kupione' }).click();
		await expect(page.getByText('Usunięto kupione: 1.')).toBeVisible();
		expect(api.calls.map((c) => c.key)).toContain(
			`POST /fridges/${fridgeId}/shopping-list/complete`
		);
	});

	test('copies the unchecked items as text', async ({ page, context }) => {
		await context.grantPermissions(['clipboard-read', 'clipboard-write']);
		await mockApi(page, handlers());
		await page.goto('/lista-zakupow');
		await page.getByRole('button', { name: 'Kopiuj' }).click();
		await expect(page.getByText('Skopiowano listę.')).toBeVisible();
		const text = await page.evaluate(() => navigator.clipboard.readText());
		expect(text).toBe('Lista zakupów — Moja lodówka\n\nMleko — 1,5 l\npapier do pieczenia');
	});

	test('adds a free-text item', async ({ page }) => {
		const api = await mockApi(page, handlers());
		await page.goto('/lista-zakupow');
		await page.getByRole('button', { name: 'Dodaj pozycję' }).click();
		const dialog = page.getByRole('dialog', { name: 'Dodaj do listy zakupów' });
		await dialog.getByRole('button', { name: 'Dowolny tekst' }).click();
		await dialog.getByLabel('Pozycja').fill('serwetki');
		await dialog.getByRole('button', { name: 'Dodaj pozycję' }).click();
		await expect(page.getByText('serwetki')).toBeVisible();
		expect(api.calls.find((c) => c.key.endsWith('/shopping-list/items') && c.json)!.json).toEqual({
			items: [{ text: 'serwetki' }]
		});
	});

	test('has no axe violations (360 px)', async ({ page }) => {
		await page.setViewportSize({ width: 360, height: 740 });
		await page.emulateMedia({ reducedMotion: 'reduce' });
		await mockApi(page, handlers());
		await page.goto('/lista-zakupow');
		await expect(page.getByRole('checkbox')).toHaveCount(3);
		await page.evaluate(() => document.fonts.ready);
		const result = await new AxeBuilder({ page })
			.withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
			.analyze();
		expect(
			result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' '))}`)
		).toEqual([]);
	});
});
