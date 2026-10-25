import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { mockApi, type MockHandlers } from './mocks/index';
import { bartek, paged, recipeId, recipesHandlers, summary, variantSummary } from './mocks/recipes';
import type { Schemas } from './mocks/types';
import { matrix, variantA, variantB, variantDetail, variantsHandlers } from './mocks/variants';

const base: MockHandlers = {
	...signedIn,
	...catalogHandlers,
	...recipesHandlers,
	...variantsHandlers
};
const url = `/przepisy/${recipeId}`;

/** The matrix without the deleted variants (their options go when nobody uses them). */
function matrixWithout(deleted: Set<string>): Schemas['VariantMatrix'] {
	const full = matrix();
	return {
		...full,
		variants: full.variants.filter((v) => !deleted.has(v.id)),
		slots: full.slots.map((slot) => ({
			...slot,
			options: slot.options
				.map((o) => ({ ...o, variantIds: o.variantIds.filter((id) => !deleted.has(id)) }))
				.filter((o) => o.variantIds.length > 0)
		}))
	};
}

/** Handlers where deleting a variant is remembered by the matrix and visibility changes by the list. */
function stateful(): { handlers: MockHandlers; deleted: Set<string> } {
	const deleted = new Set<string>();
	const visibility: Record<string, Schemas['Visibility']> = {};
	const list = () =>
		(variantsHandlers['GET /recipes/:id/variants'] as { body: Schemas['VariantSummary'][] }).body
			.filter((v) => !deleted.has(v.id))
			.map((v) => ({ ...v, visibility: visibility[v.id] ?? v.visibility }));
	return {
		deleted,
		handlers: {
			...base,
			'GET /recipes/:id/variant-matrix': () => ({ body: matrixWithout(deleted) }),
			'GET /recipes/:id/variants': () => ({ body: list() }),
			'DELETE /variants/:id': ({ params }) => {
				deleted.add(params.id ?? '');
				return { status: 204 };
			},
			'PUT /variants/:id/visibility': ({ params, json }) => {
				const next = (json as { visibility: Schemas['Visibility'] }).visibility;
				visibility[params.id ?? ''] = next;
				return { body: variantDetail({ id: params.id ?? '', visibility: next }) };
			}
		}
	};
}

const variantsTab = (page: Page) => page.getByRole('tab', { name: /^Warianty/ });
const menuOf = (page: Page, name: string) =>
	page.getByRole('button', { name: `Akcje wariantu „${name}”` });
const entry = (page: Page, name: string) =>
	page.getByRole('list', { name: 'Warianty przepisu' }).getByRole('listitem').filter({
		hasText: name
	});

test.describe('variant menu in the Warianty tab', () => {
	test('changing visibility updates the entry in place', async ({ page }) => {
		const { handlers } = stateful();
		const api = await mockApi(page, handlers);
		await page.goto(url);
		await variantsTab(page).click();
		await expect(entry(page, 'Wersja wege')).toContainText('Prywatny');

		await menuOf(page, 'Wersja wege').click();
		await page.getByRole('button', { name: 'Zmień widoczność' }).click();
		await expect(page.getByRole('dialog', { name: 'Zmień widoczność' })).toBeVisible();
		await page.getByRole('radio', { name: /Moje lodówki/ }).check({ force: true });
		await page
			.getByRole('dialog', { name: 'Zmień widoczność' })
			.getByRole('button', { name: 'Zmień widoczność' })
			.click();

		await expect(entry(page, 'Wersja wege')).toContainText('Moje lodówki');
		const put = api.calls.find((c) => c.key === `PUT /variants/${variantA}/visibility`);
		expect(put?.json).toEqual({ visibility: 'fridges' });
	});

	test('deleting the shown variant confirms, toasts, shows the base and lowers the count', async ({
		page
	}) => {
		const { handlers } = stateful();
		const api = await mockApi(page, handlers);
		await page.goto(`${url}?wariant=${variantA}`);
		await expect(variantsTab(page)).toHaveText(/Warianty \(3\)/);
		await variantsTab(page).click();
		await expect(entry(page, 'Wersja wege')).toContainText('wyświetlany');

		await menuOf(page, 'Wersja wege').click();
		await page.getByRole('button', { name: 'Usuń wariant' }).click();
		const dialog = page.getByRole('alertdialog', { name: 'Usunąć wariant „Wersja wege”?' });
		await expect(dialog).toBeVisible();
		await dialog.getByRole('button', { name: 'Usuń wariant' }).click();

		await expect(page.getByRole('status').filter({ hasText: 'Usunięto wariant.' })).toBeVisible();
		await expect(variantsTab(page)).toHaveText(/Warianty \(2\)/);
		await expect(entry(page, 'Wersja wege')).toHaveCount(0);
		await expect(entry(page, 'Przepis bazowy')).toContainText('wyświetlany');
		await expect(page).toHaveURL(new RegExp(`${recipeId}$`));
		expect(api.calls.map((c) => c.key)).toContain(`DELETE /variants/${variantA}`);
	});

	test('deleting another variant keeps the shown one', async ({ page }) => {
		const { handlers } = stateful();
		await mockApi(page, handlers);
		await page.goto(`${url}?wariant=${variantB}`);
		await variantsTab(page).click();

		await menuOf(page, 'Wersja wege').click();
		await page.getByRole('button', { name: 'Usuń wariant' }).click();
		await page.getByRole('alertdialog').getByRole('button', { name: 'Usuń wariant' }).click();

		await expect(variantsTab(page)).toHaveText(/Warianty \(2\)/);
		await expect(entry(page, 'Kremowa')).toContainText('wyświetlany');
		await expect(page).toHaveURL(new RegExp(`\\?wariant=${variantB}$`));
	});

	test('Edytuj wariant opens the editor', async ({ page }) => {
		const { handlers } = stateful();
		await mockApi(page, handlers);
		await page.goto(url);
		await variantsTab(page).click();
		await menuOf(page, 'Kremowa').click();
		await page.getByRole('link', { name: 'Edytuj wariant' }).click();
		await expect(page).toHaveURL(new RegExp(`/warianty/${variantB}/edytuj$`));
	});

	test("another user's variant has no menu", async ({ page }) => {
		const { handlers } = stateful();
		const foreign = matrix();
		foreign.variants = foreign.variants.map((v) =>
			v.id === variantB ? { ...v, author: bartek } : v
		);
		await mockApi(page, { ...handlers, 'GET /recipes/:id/variant-matrix': { body: foreign } });
		await page.goto(url);
		await variantsTab(page).click();
		await expect(entry(page, 'Kremowa')).toBeVisible();
		await expect(menuOf(page, 'Kremowa')).toHaveCount(0);
		await expect(menuOf(page, 'Wersja wege')).toBeVisible();
	});

	test('the menu button is at least 44 px', async ({ page }) => {
		const { handlers } = stateful();
		await mockApi(page, handlers);
		await page.goto(url);
		await variantsTab(page).click();
		const box = await menuOf(page, 'Wersja wege').boundingBox();
		expect(box?.width).toBeGreaterThanOrEqual(44);
		expect(box?.height).toBeGreaterThanOrEqual(44);
	});

	for (const colorScheme of ['light', 'dark'] as const) {
		test(`the open menu and the confirmation pass axe (${colorScheme})`, async ({ page }) => {
			await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
			const { handlers } = stateful();
			await mockApi(page, handlers);
			await page.goto(url);
			await variantsTab(page).click();
			await menuOf(page, 'Wersja wege').click();
			await expect(page.getByRole('button', { name: 'Usuń wariant' })).toBeVisible();
			expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);

			await page.getByRole('button', { name: 'Usuń wariant' }).click();
			await expect(page.getByRole('alertdialog')).toBeVisible();
			expect((await new AxeBuilder({ page }).analyze()).violations).toEqual([]);
		});
	}
});

test.describe('variant menu on cards (Moje warianty)', () => {
	const second = '00000000-0000-7000-8000-0000000b0002';
	const cards: MockHandlers = {
		...base,
		'GET /variants': {
			body: paged([
				variantSummary({ visibility: 'private' }),
				variantSummary({ id: second, name: 'Utracony', baseAvailable: false })
			])
		},
		'DELETE /variants/:id': { status: 204 },
		'PUT /variants/:id/visibility': ({ params, json }) => ({
			body: variantDetail({
				id: params.id ?? '',
				visibility: (json as { visibility: Schemas['Visibility'] }).visibility
			})
		})
	};

	async function open(page: Page) {
		const api = await mockApi(page, cards);
		await page.goto('/przepisy');
		await page.getByRole('tab', { name: 'Moje warianty' }).click();
		return api;
	}

	test('delete removes the card; a card without a base offers only delete', async ({ page }) => {
		const api = await open(page);
		const lost = page.getByRole('article').filter({ hasText: 'Utracony' });
		await expect(lost.getByRole('link')).toHaveCount(0);
		await menuOf(page, 'Utracony').click();
		await expect(page.getByRole('link', { name: 'Edytuj wariant' })).toHaveCount(0);
		await expect(page.getByRole('button', { name: 'Zmień widoczność' })).toHaveCount(0);
		await page.getByRole('button', { name: 'Usuń wariant' }).click();
		await page
			.getByRole('alertdialog', { name: 'Usunąć wariant „Utracony”?' })
			.getByRole('button', { name: 'Usuń wariant' })
			.click();
		await expect(lost).toHaveCount(0);
		expect(api.calls.map((c) => c.key)).toContain(`DELETE /variants/${second}`);
		await expect(page.getByRole('status').filter({ hasText: 'Usunięto wariant.' })).toBeVisible();
	});

	test('change visibility saves with the new endpoint', async ({ page }) => {
		const api = await open(page);
		await menuOf(page, 'Wersja wege').click();
		await page.getByRole('button', { name: 'Zmień widoczność' }).click();
		await page.getByRole('radio', { name: /Moje lodówki/ }).check({ force: true });
		await page
			.getByRole('dialog', { name: 'Zmień widoczność' })
			.getByRole('button', { name: 'Zmień widoczność' })
			.click();
		await expect(
			page.getByRole('status').filter({ hasText: 'Zmieniono widoczność.' })
		).toBeVisible();
		const put = api.calls.find((c) => c.key.startsWith('PUT /variants/'));
		expect(put?.json).toEqual({ visibility: 'fridges' });
	});

	test("another user's favourited variant has no menu", async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /favorites': {
				body: paged([
					{
						kind: 'variant' as const,
						favoritedAt: '2026-10-04T10:00:00Z',
						variant: variantSummary({ isFavorite: true, author: bartek }),
						recipe: summary()
					}
				])
			}
		});
		await page.goto('/przepisy?tab=mine&sub=favorites');
		await expect(page.getByRole('article').filter({ hasText: 'Wersja wege' })).toBeVisible();
		await expect(menuOf(page, 'Wersja wege')).toHaveCount(0);
	});
});
