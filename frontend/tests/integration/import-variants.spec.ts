import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { mockApi, type MockHandlers } from './mocks/index';
import { detail, newRecipeId, recipesHandlers, sampleExport } from './mocks/recipes';
import {
	importedVariantId,
	importMatches,
	publicMatchId,
	transferHandlers,
	variantImportResult
} from './mocks/transfer';

// Importing a file whose recipe already exists (PROJECT §4.7, D-35).
// The transfer handlers come first, so `GET /recipes/:id` does not answer the matches route; tests
// override them by spreading `base` first (the key keeps its place, the value is replaced).
const base: MockHandlers = {
	...transferHandlers(),
	...signedIn,
	...catalogHandlers,
	...recipesHandlers,
	'POST /recipes/import': {
		status: 201,
		body: detail({ id: newRecipeId, title: 'Zaimportowany przepis' })
	}
};

async function pickFile(page: Page, content: string = JSON.stringify(sampleExport)) {
	await page.goto('/przepisy?import=1');
	const dialog = page.getByRole('dialog', { name: 'Importuj przepis' });
	await dialog.getByLabel('Plik z przepisem').setInputFiles({
		name: 'export.json',
		mimeType: 'application/json',
		buffer: Buffer.from(content)
	});
	await dialog.getByRole('button', { name: 'Importuj' }).click();
}

function choiceDialog(page: Page) {
	return page.getByRole('dialog', { name: 'Ten przepis już istnieje' });
}

test('without matching recipes the file is imported as a new recipe', async ({ page }) => {
	const api = await mockApi(page, { ...base, ...transferHandlers([]) });

	await pickFile(page);

	await expect(page).toHaveURL(new RegExp(`/przepisy/${newRecipeId}$`));
	const keys = api.calls.map((c) => c.key);
	expect(keys).toContain('GET /recipes/import-matches');
	expect(keys).toContain('POST /recipes/import');
	expect(keys.some((k) => k.endsWith('/variants/import'))).toBe(false);
});

test('asks for the matching title and lists the matches', async ({ page }) => {
	const titles: (string | null)[] = [];
	await mockApi(page, {
		...base,
		'GET /recipes/import-matches': ({ url }) => {
			titles.push(url.searchParams.get('title'));
			return { body: importMatches };
		}
	});

	await pickFile(page);

	const dialog = choiceDialog(page);
	await expect(dialog).toBeVisible();
	expect(titles).toEqual([sampleExport.recipe.title]);
	const matches = dialog.getByRole('list', { name: 'Przepisy o tym tytule' }).getByRole('listitem');
	await expect(matches).toHaveCount(2);
	await expect(matches.nth(0)).toContainText('@ania · Prywatny');
	await expect(matches.nth(1)).toContainText('@bartek · Publiczny');
	await expect(dialog.getByRole('button', { name: 'Importuj jako nowy przepis' })).toBeVisible();
	await expect(dialog.getByRole('button', { name: 'Anuluj', exact: true })).toBeVisible();
});

test('"Dodaj jako wariant" imports into the match and opens the first new variant', async ({
	page
}) => {
	const api = await mockApi(page, base);
	await pickFile(page);

	const match = choiceDialog(page).getByRole('listitem').filter({ hasText: '@bartek' });
	await match.getByRole('button', { name: 'Dodaj jako wariant' }).click();

	await expect(page).toHaveURL(
		new RegExp(`/przepisy/${publicMatchId}\\?wariant=${importedVariantId}$`)
	);
	const post = api.calls.find((c) => c.key === `POST /recipes/${publicMatchId}/variants/import`);
	expect(post?.json).toEqual(sampleExport);
	expect(api.calls.some((c) => c.key === 'POST /recipes/import')).toBe(false);
});

test('when everything is skipped it stays and says so', async ({ page }) => {
	await mockApi(page, {
		...base,
		'POST /recipes/:id/variants/import': ({ params }) => ({
			status: 200,
			body: variantImportResult(params.id as string, { variants: [], skipped: 3 })
		})
	});
	await pickFile(page);

	await choiceDialog(page)
		.getByRole('listitem')
		.first()
		.getByRole('button', { name: 'Dodaj jako wariant' })
		.click();

	await expect(page.getByText('Te warianty już istnieją (pominięto 3)')).toBeVisible();
	await expect(choiceDialog(page)).toBeVisible();
	await expect(page).toHaveURL(/\/przepisy$/);
});

test('"Importuj jako nowy przepis" runs the normal import', async ({ page }) => {
	const api = await mockApi(page, base);
	await pickFile(page);

	await choiceDialog(page).getByRole('button', { name: 'Importuj jako nowy przepis' }).click();

	await expect(page).toHaveURL(new RegExp(`/przepisy/${newRecipeId}$`));
	expect(api.calls.find((c) => c.key === 'POST /recipes/import')?.json).toEqual(sampleExport);
});

test('"Anuluj" closes without importing', async ({ page }) => {
	const api = await mockApi(page, base);
	await pickFile(page);

	await choiceDialog(page).getByRole('button', { name: 'Anuluj', exact: true }).click();

	await expect(choiceDialog(page)).toBeHidden();
	expect(api.calls.some((c) => c.key.startsWith('POST '))).toBe(false);
});

test('a file that is not JSON shows the import error without looking for matches', async ({
	page
}) => {
	const api = await mockApi(page, base);

	await pickFile(page, 'not json');

	await expect(
		page
			.getByRole('dialog', { name: 'Importuj przepis' })
			.getByText('Ten plik nie jest prawidłowym eksportem przepisu.')
	).toBeVisible();
	expect(api.calls.some((c) => c.key === 'GET /recipes/import-matches')).toBe(false);
});

for (const colorScheme of ['light', 'dark'] as const) {
	test(`the choice step has no axe violations (${colorScheme})`, async ({ page }) => {
		await page.emulateMedia({ colorScheme, reducedMotion: 'reduce' });
		await mockApi(page, base);
		await pickFile(page);
		await expect(choiceDialog(page)).toBeVisible();
		await page.evaluate(() => document.fonts.ready);

		const result = await new AxeBuilder({ page })
			.withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
			.analyze();

		expect(result.violations).toEqual([]);
	});
}
