import { expect, test } from '@playwright/test';
import { authConfig, me, signedIn } from './mocks/auth';
import { mockApi, problem } from './mocks/index';

test('the app loads /przepisy for a signed-in user', async ({ page }) => {
	await mockApi(page, signedIn);

	await page.goto('/przepisy');

	await expect(page.getByRole('heading', { level: 1, name: 'Przepisy' })).toBeVisible();
	await expect(page.getByRole('navigation', { name: 'Nawigacja główna' })).toBeVisible();
	await expect(page.getByRole('link', { name: 'Lodówka' })).toBeVisible();
});

test('an anonymous user is sent to the login page and can use dev-login', async ({ page }) => {
	let session = false;
	const api = await mockApi(page, {
		'GET /me': () => (session ? { body: me } : problem(401, 'auth.required')),
		'GET /auth/config': { body: authConfig },
		'GET /fridges': { body: [] },
		'POST /auth/dev-login': () => {
			session = true;
			return { body: me };
		}
	});

	await page.goto('/lodowka');
	await expect(page).toHaveURL(/\/logowanie\?next=%2Flodowka$/);

	await page.getByLabel('E-mail').fill('ania@example.test');
	await page.getByLabel('Pseudonim (opcjonalnie)').fill('ania');
	await page.getByRole('button', { name: 'Zaloguj się' }).click();

	await expect(page).toHaveURL(/\/lodowka$/);
	await expect(page.getByRole('heading', { level: 1, name: 'Lodówka' })).toBeVisible();
	const login = api.calls.find((c) => c.key === 'POST /auth/dev-login');
	expect(login?.json).toEqual({ email: 'ania@example.test', nickname: 'ania' });
});

test('a user without a nickname is sent to onboarding', async ({ page }) => {
	await mockApi(page, { 'GET /me': { body: { ...me, nickname: null } } });

	await page.goto('/przepisy');

	await expect(page).toHaveURL(/\/witaj$/);
	await expect(page.getByRole('heading', { level: 1, name: 'Witaj!' })).toBeVisible();
});

test('an unknown address shows the error page with the usual page header', async ({ page }) => {
	await mockApi(page, signedIn);

	await page.goto('/nie-ma-takiej-strony');

	await expect(page.getByRole('heading', { level: 1, name: 'Coś poszło nie tak' })).toBeVisible();
	await expect(page.getByRole('navigation', { name: 'Nawigacja główna' })).toBeVisible();
});
