import AxeBuilder from '@axe-core/playwright';
import { expect, test } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers, tagObiad } from './mocks/catalog';
import { mockApi, type MockHandlers } from './mocks/index';
import {
	ania,
	bartek,
	paged,
	recipeId,
	recipesHandlers,
	summary,
	variantSummary
} from './mocks/recipes';

const base: MockHandlers = { ...signedIn, ...catalogHandlers, ...recipesHandlers };

const variantId = '00000000-0000-7000-8000-0000000b0001';
const otherRecipeId = '00000000-0000-7000-8000-0000000e0002';

test.describe('the catalog', () => {
	test('lists recipe cards with their details', async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /recipes': {
				body: paged([
					summary({ variantCount: 3, visibility: 'public', author: bartek }),
					summary({
						id: otherRecipeId,
						title: 'Zupa pomidorowa',
						visibility: 'fridges',
						prepMinutes: null,
						cookMinutes: 40
					})
				])
			}
		});
		await page.goto('/przepisy');

		await expect(page.getByRole('heading', { level: 1, name: 'Przepisy' })).toBeVisible();
		const card = page.getByRole('article').filter({ hasText: 'Placki ziemniaczane' });
		await expect(card.getByRole('link', { name: 'Placki ziemniaczane' })).toHaveAttribute(
			'href',
			`/przepisy/${recipeId}`
		);
		await expect(card).toContainText('autor: bartek');
		await expect(card).toContainText('Przygotowanie 15 min');
		await expect(card).toContainText('3 warianty');
		await expect(card.getByRole('img', { name: 'Publiczny' })).toBeVisible();
		await expect(card.getByText('obiad')).toBeVisible();
		await expect(
			page
				.getByRole('article')
				.filter({ hasText: 'Zupa pomidorowa' })
				.getByRole('img', { name: 'Widoczny w moich lodówkach' })
		).toBeVisible();
	});

	test('searches with a debounce and filters by tag', async ({ page }) => {
		const api = await mockApi(page, base);
		await page.goto('/przepisy');
		await expect(page.getByRole('article')).toHaveCount(1);

		await page.getByLabel('Szukaj przepisów').fill('placki');
		await expect
			.poll(() => api.calls.filter((c) => c.key === 'GET /recipes').length)
			.toBeGreaterThan(1);
		await page.getByRole('button', { name: 'obiad' }).click();
		await expect(page).toHaveURL(new RegExp(`q=placki&tags=${tagObiad.id}`));
		await expect(page.getByRole('button', { name: 'obiad' })).toHaveAttribute(
			'aria-pressed',
			'true'
		);
	});

	test('shows an empty state with an action', async ({ page }) => {
		await mockApi(page, { ...base, 'GET /recipes': { body: paged([]) } });
		await page.goto('/przepisy');
		await expect(page.getByRole('heading', { name: 'Brak przepisów' })).toBeVisible();
		await expect(page.getByRole('link', { name: 'Nowy przepis' }).last()).toHaveAttribute(
			'href',
			'/przepisy/nowy'
		);
	});

	test('"Pokaż więcej" loads the next page', async ({ page }) => {
		await mockApi(page, {
			...base,
			'GET /recipes': ({ url }) => {
				const pageNo = Number(url.searchParams.get('page') ?? '1');
				const item =
					pageNo === 1 ? summary() : summary({ id: otherRecipeId, title: 'Drugi przepis' });
				return { body: { items: [item], page: pageNo, pageSize: 1, totalCount: 2 } };
			}
		});
		await page.goto('/przepisy');
		await page.getByRole('button', { name: 'Pokaż więcej' }).click();
		await expect(page.getByRole('article')).toHaveCount(2);
		await expect(page.getByRole('button', { name: 'Pokaż więcej' })).toHaveCount(0);
	});

	test('Moje przepisy → Utworzone asks for author=me', async ({ page }) => {
		const api = await mockApi(page, base);
		await page.goto('/przepisy');
		await page.getByRole('tab', { name: 'Moje przepisy' }).click();
		await expect(page.getByRole('button', { name: 'Utworzone' })).toHaveAttribute(
			'aria-pressed',
			'true'
		);
		await expect.poll(() => api.calls.some((c) => c.key === 'GET /recipes')).toBe(true);
		await expect(page).toHaveURL(/tab=mine/);
	});

	test('toggles ♥ on a card', async ({ page }) => {
		const api = await mockApi(page, base);
		await page.goto('/przepisy');
		const heart = page.getByRole('button', { name: 'Dodaj do ulubionych' });
		await heart.click();
		await expect(page.getByRole('button', { name: 'Usuń z ulubionych' })).toHaveAttribute(
			'aria-pressed',
			'true'
		);
		expect(api.calls.map((c) => c.key)).toContain(`PUT /recipes/${recipeId}/favorite`);
		await page.getByRole('button', { name: 'Usuń z ulubionych' }).click();
		await expect(page.getByRole('button', { name: 'Dodaj do ulubionych' })).toBeVisible();
		expect(api.calls.map((c) => c.key)).toContain(`DELETE /recipes/${recipeId}/favorite`);
	});
});

test.describe('Moje warianty and Ulubione', () => {
	const favorites: MockHandlers = {
		...base,
		'GET /favorites': {
			body: paged([
				{
					kind: 'recipe' as const,
					favoritedAt: '2026-10-05T10:00:00Z',
					recipe: summary({ isFavorite: true })
				},
				{
					kind: 'variant' as const,
					favoritedAt: '2026-10-04T10:00:00Z',
					variant: variantSummary({ isFavorite: true, author: bartek }),
					recipe: summary({ id: otherRecipeId, title: 'Zupa pomidorowa', author: ania })
				}
			])
		}
	};

	test('Moje warianty links to the recipe with ?wariant=', async ({ page }) => {
		const api = await mockApi(page, {
			...base,
			'GET /variants': {
				body: paged([
					variantSummary(),
					variantSummary({
						id: '00000000-0000-7000-8000-0000000b0002',
						name: 'Utracony',
						baseAvailable: false
					})
				])
			},
			'DELETE /variants/:id': { status: 204 }
		});
		await page.goto('/przepisy');
		await page.getByRole('tab', { name: 'Moje warianty' }).click();

		await expect(page.getByRole('link', { name: 'Wersja wege' })).toHaveAttribute(
			'href',
			`/przepisy/${recipeId}?wariant=${variantId}`
		);
		const lost = page.getByRole('article').filter({ hasText: 'Utracony' });
		await expect(lost).toContainText('Przepis bazowy jest niedostępny');
		await expect(lost.getByRole('link')).toHaveCount(0);
		expect(api.calls.some((c) => c.key === 'GET /variants')).toBe(true);

		await lost.getByRole('button', { name: 'Usuń wariant' }).click();
		await page
			.getByRole('alertdialog', { name: 'Usunąć wariant?' })
			.getByRole('button', { name: 'Usuń wariant' })
			.click();
		await expect(lost).toHaveCount(0);
		expect(api.calls.map((c) => c.key)).toContain(
			'DELETE /variants/00000000-0000-7000-8000-0000000b0002'
		);
	});

	test('Ulubione mixes recipe and variant cards; ♥ off removes the card with undo', async ({
		page
	}) => {
		const api = await mockApi(page, favorites);
		await page.goto('/przepisy?tab=mine&sub=favorites');

		await expect(page.getByRole('article')).toHaveCount(2);
		const variantCard = page.getByRole('article').filter({ hasText: 'Wersja wege' });
		await expect(variantCard).toContainText(
			'wariant „Wersja wege” · przepis „Placki ziemniaczane”'
		);
		await expect(variantCard.getByRole('link')).toHaveAttribute('href', /\?wariant=/);

		await variantCard.getByRole('button', { name: 'Usuń z ulubionych' }).click();
		await expect(page.getByRole('article')).toHaveCount(1);
		expect(api.calls.map((c) => c.key)).toContain(`DELETE /variants/${variantId}/favorite`);

		const toast = page.getByRole('status').filter({ hasText: 'Usunięto z ulubionych.' });
		await toast.getByRole('button', { name: 'Cofnij' }).click();
		await expect(page.getByRole('article')).toHaveCount(2);
		expect(api.calls.map((c) => c.key)).toContain(`PUT /variants/${variantId}/favorite`);
	});
});

test.describe('accessibility', () => {
	for (const colorScheme of ['light', 'dark'] as const) {
		test(`the catalog has no axe violations (${colorScheme})`, async ({ page }) => {
			await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
			await mockApi(page, base);
			await page.goto('/przepisy');
			await expect(page.getByRole('article')).toHaveCount(1);
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
