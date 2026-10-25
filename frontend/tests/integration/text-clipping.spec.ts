import { expect, test, type Locator } from '@playwright/test';
import { me, signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import {
	fridgeHandlers,
	fridgeId,
	item,
	otherSummary,
	summary as fridgeSummary,
	creamItem
} from './mocks/fridge';
import { mockApi, type MockHandlers } from './mocks/index';
import { profileMock } from './mocks/profile';
import { paged, recipeId, recipesHandlers, summary as recipeSummary } from './mocks/recipes';
import { baseRecipe, variantsHandlers } from './mocks/variants';

// Descenders (y, j, g, ą, ę) must stay inside each element's clip box. `truncate` clips at the
// padding box, so its height must be at least the font's ascent + descent. The fix lives in
// app.css (`.truncate`), not in the components.

const title = 'Gęsty jogurt pyszny';

const base: MockHandlers = { ...signedIn, ...catalogHandlers, ...recipesHandlers };

/** The padding box height of `el` and the font's ascent + descent for its computed font. */
async function expectNoClipping(el: Locator) {
	await expect(el).toBeVisible();
	const { box, ink } = await el.evaluate((node) => {
		const cs = getComputedStyle(node);
		const ctx = document.createElement('canvas').getContext('2d');
		if (!ctx) throw new Error('no 2d context');
		ctx.font = `${cs.fontStyle} ${cs.fontWeight} ${cs.fontSize} ${cs.fontFamily}`;
		const m = ctx.measureText(node.textContent ?? 'Ay');
		return {
			box: node.clientHeight,
			ink: m.fontBoundingBoxAscent + m.fontBoundingBoxDescent
		};
	});
	expect(box, 'clip box must hold the font ascent + descent').toBeGreaterThanOrEqual(ink);
}

test.describe('recipe page', () => {
	for (const t of [title, 'Bardzo długi tytuł przepisu, który nie mieści się w jednej linii']) {
		test(`h1 "${t.slice(0, 20)}" is not clipped`, async ({ page }) => {
			await mockApi(page, {
				...base,
				...variantsHandlers,
				'GET /recipes/:id': { body: baseRecipe({ title: t }) }
			});
			await page.goto(`/przepisy/${recipeId}`);
			await expectNoClipping(page.getByRole('heading', { level: 1, name: t }));
		});
	}
});

test('recipe card title on /przepisy is not clipped', async ({ page }) => {
	await mockApi(page, {
		...base,
		'GET /recipes': { body: paged([recipeSummary({ title })]) }
	});
	await page.goto('/przepisy');
	// The card title wraps (no truncate), so nothing clips it: no ancestor may clip overflow.
	const clipped = await page
		.getByText(title, { exact: true })
		.first()
		.evaluate((node) => {
			for (let el: Element | null = node; el; el = el.parentElement) {
				const o = getComputedStyle(el).overflow;
				if (o.includes('hidden') || o.includes('clip')) return el.className;
			}
			return null;
		});
	expect(clipped, 'an ancestor clips the card title').toBeNull();
});

test('profile nickname is not clipped', async ({ page }) => {
	const mock = profileMock({ ...me, nickname: title });
	await mockApi(page, mock.handlers);
	await page.goto('/profil');
	await expectNoClipping(page.getByText(title, { exact: true }).first());
});

test('Select trigger text on /profil is not clipped', async ({ page }) => {
	const mock = profileMock();
	await mockApi(page, {
		...mock.handlers,
		'GET /fridges': { body: [{ ...fridgeSummary, name: title }, otherSummary] }
	});
	await page.goto('/profil');
	await expectNoClipping(page.getByText(title, { exact: true }).first());
});

test('fridge item note is not clipped', async ({ page }) => {
	const noted = item('00000000-0000-7000-8000-0000000d0009', creamItem.product, 330, 70, {
		note: title
	});
	await mockApi(page, { ...base, ...fridgeHandlers([noted]) });
	await page.goto(`/lodowka/${fridgeId}`);
	await expectNoClipping(page.getByText(title, { exact: true }).first());
});

test('side rail label is not clipped (desktop)', async ({ page, isMobile }) => {
	test.skip(isMobile, 'the side rail is only shown from 1024 px');
	await mockApi(page, { ...base, ...variantsHandlers });
	await page.goto('/przepisy');
	await expectNoClipping(page.getByRole('link', { name: 'Przepisy' }).locator('span.truncate'));
});
