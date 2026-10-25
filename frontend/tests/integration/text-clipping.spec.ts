import { expect, test, type Locator } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { mockApi, type MockHandlers } from './mocks/index';
import { recipeId, recipesHandlers } from './mocks/recipes';
import { baseRecipe, variantsHandlers } from './mocks/variants';

// Descenders (y, j, g, ą, ę) must stay inside the element's box. A box shorter than the font's
// ascent + descent clips them, so every fixed element must be at least that tall.

const handlers = (title: string): MockHandlers => ({
	...signedIn,
	...catalogHandlers,
	...recipesHandlers,
	...variantsHandlers,
	'GET /recipes/:id': { body: baseRecipe({ title }) }
});

/** The content-box height of `el` and the font's ascent + descent for its computed font. */
async function inkVsBox(el: Locator) {
	return el.evaluate((node) => {
		const cs = getComputedStyle(node);
		const ctx = document.createElement('canvas').getContext('2d');
		if (!ctx) throw new Error('no 2d context');
		ctx.font = `${cs.fontStyle} ${cs.fontWeight} ${cs.fontSize} ${cs.fontFamily}`;
		const m = ctx.measureText(node.textContent ?? 'Ay');
		const ink = m.fontBoundingBoxAscent + m.fontBoundingBoxDescent;
		const rect = node.getBoundingClientRect();
		const padding = parseFloat(cs.paddingTop) + parseFloat(cs.paddingBottom);
		return { box: rect.height - padding, ink };
	});
}

async function expectNoClipping(el: Locator) {
	await expect(el).toBeVisible();
	const { box, ink } = await inkVsBox(el);
	expect(box, 'line box must hold the font ascent + descent').toBeGreaterThanOrEqual(ink);
}

const titles = [
	'Gęsty jogurt pyszny',
	'Bardzo długi tytuł przepisu, który nie mieści się w jednej linii'
];

for (const title of titles) {
	test(`recipe title "${title}" is not clipped`, async ({ page }) => {
		await mockApi(page, handlers(title));
		await page.goto(`/przepisy/${recipeId}`);
		await expectNoClipping(page.getByRole('heading', { level: 1, name: title }));
	});
}
