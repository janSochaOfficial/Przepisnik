import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { mockApi } from './mocks/index';

// The dev-only UI showcase (task F0) renders every primitive in every state. It must have no axe
// violations (WCAG 2.1 A/AA) in light and dark mode, with its overlays open too.

const WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

async function expectNoViolations(page: Page) {
	const result = await new AxeBuilder({ page }).withTags(WCAG).analyze();
	const summary = result.violations.map(
		(v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' ')).join(', ')}`
	);
	expect(summary).toEqual([]);
}

async function openShowcase(page: Page) {
	await mockApi(page, signedIn);
	await page.goto('/ui');
	await expect(page.getByRole('heading', { level: 1, name: 'Elementy interfejsu' })).toBeVisible();
	// Fonts change metrics; wait so contrast and layout are checked as the user sees them.
	await page.evaluate(() => document.fonts.ready);
}

for (const colorScheme of ['light', 'dark'] as const) {
	test.describe(`${colorScheme} mode`, () => {
		// Reduced motion: axe must not sample colours mid-animation (toasts fade in).
		test.use({ colorScheme, reducedMotion: 'reduce' });

		test('the showcase has no accessibility violations', async ({ page }) => {
			await openShowcase(page);
			await expectNoViolations(page);
		});

		test('open overlays have no accessibility violations', async ({ page }) => {
			// Several full axe scans: ~4 s alone, but over 30 s when the whole suite runs in parallel.
			test.slow();
			await openShowcase(page);

			await page.getByRole('button', { name: 'Wybierz opcję' }).click();
			const sheet = page.getByRole('dialog', { name: 'Zamiennik składnika' });
			await expect(sheet).toBeVisible();
			await expectNoViolations(page);
			await page.keyboard.press('Escape');
			await expect(sheet).toBeHidden();

			await page.getByRole('button', { name: 'Otwórz okno' }).click();
			const dialog = page.getByRole('dialog', { name: 'Odejmij z lodówki' });
			await expect(dialog).toBeVisible();
			await expectNoViolations(page);
			await page.keyboard.press('Escape');
			await expect(dialog).toBeHidden();

			await page.getByRole('button', { name: 'Usuń i pozwól cofnąć' }).click();
			await expect(page.getByRole('button', { name: 'Cofnij' })).toBeVisible();
			await expectNoViolations(page);
		});
	});
}

test('the navigation is a bottom tab bar on phones and a side rail on desktop', async ({
	page
}, testInfo) => {
	await openShowcase(page);
	const nav = page.getByRole('navigation', { name: 'Nawigacja główna' });
	const box = await nav.boundingBox();
	const viewport = page.viewportSize();
	if (!box || !viewport) throw new Error('no layout');

	if (testInfo.project.name === 'mobile') {
		expect(Math.round(box.y + box.height)).toBe(viewport.height);
		expect(box.width).toBe(viewport.width);
	} else {
		expect(box.x).toBe(0);
		expect(box.height).toBe(viewport.height);
	}
	for (const name of ['Przepisy', 'Lodówka', 'Lista zakupów', 'Profil']) {
		const link = nav.getByRole('link', { name, exact: true });
		await expect(link).toBeVisible();
		expect((await link.boundingBox())?.height).toBeGreaterThanOrEqual(44);
	}
});

test('the bottom sheet is a sheet on phones and a popover on desktop', async ({
	page
}, testInfo) => {
	await openShowcase(page);

	await page.getByRole('button', { name: 'Wybierz opcję' }).click();

	const sheet = page.getByRole('dialog', { name: 'Zamiennik składnika' });
	const shape = testInfo.project.name === 'mobile' ? 'sheet' : 'popover';
	await expect(sheet).toHaveAttribute('data-shape', shape);
	await sheet.getByRole('button', { name: 'Jogurt grecki' }).click();
	await expect(sheet).toBeHidden();
});

test('a number field takes a decimal comma', async ({ page }) => {
	await openShowcase(page);
	const amount = page.getByRole('textbox', { name: 'Ilość' }).first();

	await amount.fill('2.75');
	await amount.blur();

	await expect(amount).toHaveValue('2,75');
	await amount.fill('abc');
	await expect(page.getByText('Wpisz liczbę, np. 1,5.')).toBeVisible();
});
