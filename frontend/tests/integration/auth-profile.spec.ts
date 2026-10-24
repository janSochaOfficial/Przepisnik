import AxeBuilder from '@axe-core/playwright';
import { expect, test, type Page } from '@playwright/test';
import { me, signedIn } from './mocks/auth';
import { catalogHandlers } from './mocks/catalog';
import { fridgeHandlers, fridgeId, otherFridgeId, summary } from './mocks/fridge';
import { mockApi, problem, type MockHandlers } from './mocks/index';
import { profileMock } from './mocks/profile';
import { recipesHandlers } from './mocks/recipes';

const WCAG = ['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'];

async function expectNoViolations(page: Page) {
	const result = await new AxeBuilder({ page }).withTags(WCAG).analyze();
	const found = result.violations.map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(' '))}`);
	expect(found).toEqual([]);
}

/** A server where nobody is signed in until `POST /auth/dev-login` is called. */
function loginServer(user: typeof me) {
	const mock = profileMock(user);
	let session = false;
	const handlers: MockHandlers = {
		...mock.handlers,
		'GET /me': () => (session ? { body: mock.state.me } : problem(401, 'auth.required')),
		'POST /auth/dev-login': () => {
			session = true;
			return { body: mock.state.me };
		}
	};
	return { mock, handlers };
}

async function devLogin(page: Page) {
	await page.getByLabel('E-mail').fill('ania@example.test');
	await page.getByRole('button', { name: 'Zaloguj się' }).click();
}

test.describe('onboarding', () => {
	test('first login goes through the nickname form to the catalog', async ({ page }) => {
		const { mock, handlers } = loginServer({ ...me, nickname: null });
		const api = await mockApi(page, { ...handlers, ...catalogHandlers, ...recipesHandlers });

		await page.goto('/przepisy');
		await expect(page).toHaveURL(/\/logowanie\?next=%2Fprzepisy$/);
		await devLogin(page);

		await expect(page).toHaveURL(/\/witaj\?next=%2Fprzepisy$/);
		await expect(page.getByRole('heading', { level: 1, name: 'Witaj!' })).toBeVisible();
		const submit = page.getByRole('button', { name: 'Zacznij gotować' });
		await expect(submit).toBeDisabled();

		await page.getByRole('textbox', { name: 'Pseudonim' }).fill('nowa_kasia');
		await expect(page.getByText('Pseudonim jest wolny.').first()).toBeVisible();
		await submit.click();

		await expect(page).toHaveURL(/\/przepisy$/);
		await expect(page.getByRole('heading', { level: 1, name: 'Przepisy' })).toBeVisible();
		expect(api.calls.find((c) => c.key === 'PUT /me/nickname')?.json).toEqual({
			nickname: 'nowa_kasia'
		});
		expect(mock.state.me.nickname).toBe('nowa_kasia');
	});

	test('a taken nickname shows an inline error and blocks the form', async ({ page }) => {
		const mock = profileMock({ ...me, nickname: null });
		await mockApi(page, mock.handlers);

		await page.goto('/witaj');
		await page.getByRole('textbox', { name: 'Pseudonim' }).fill('Bartek');

		await expect(page.getByText('Ten pseudonim jest już zajęty.')).toBeVisible();
		await expect(page.getByRole('textbox', { name: 'Pseudonim' })).toHaveAttribute(
			'aria-invalid',
			'true'
		);
		await expect(page.getByRole('button', { name: 'Zacznij gotować' })).toBeDisabled();
	});

	test('a nickname that wins the availability check but loses the race is reported on save', async ({
		page
	}) => {
		const mock = profileMock({ ...me, nickname: null });
		await mockApi(page, {
			...mock.handlers,
			'GET /users/nickname-available': { body: { available: true } }
		});

		await page.goto('/witaj');
		await page.getByRole('textbox', { name: 'Pseudonim' }).fill('zosia');
		await page.getByRole('button', { name: 'Zacznij gotować' }).click();

		await expect(page.getByText('Ten pseudonim jest już zajęty.')).toBeVisible();
		await expect(page).toHaveURL(/\/witaj$/);
	});

	test('the rules are checked while typing', async ({ page }) => {
		await mockApi(page, profileMock({ ...me, nickname: null }).handlers);

		await page.goto('/witaj');
		const field = page.getByRole('textbox', { name: 'Pseudonim' });
		await field.fill('ania kowalska');
		await expect(page.getByText('Użyj tylko liter, cyfr, _ lub -, bez spacji.')).toBeVisible();
		await field.fill('a'.repeat(31));
		await expect(page.getByText('Pseudonim może mieć najwyżej 30 znaków.')).toBeVisible();
		await field.fill('ab');
		await field.blur();
		await expect(page.getByText('Pseudonim musi mieć co najmniej 3 znaki.')).toBeVisible();
	});
});

test.describe('the session', () => {
	test('a 401 from any API call sends the user to the login page and back', async ({ page }) => {
		// `/me` still answers, but the fridge call says the session is gone (e.g. expired in another tab).
		await mockApi(page, {
			...signedIn,
			...fridgeHandlers(),
			'GET /fridges': problem(401, 'auth.required')
		});

		await page.goto('/lodowka');

		await expect(page).toHaveURL(/\/logowanie\?next=%2Flodowka$/);
	});

	test('logging out ends the session and shows the login page', async ({ page }) => {
		const mock = profileMock();
		let session = true;
		const api = await mockApi(page, {
			...mock.handlers,
			'GET /me': () => (session ? { body: mock.state.me } : problem(401, 'auth.required')),
			'POST /auth/logout': () => {
				session = false;
				return { status: 204 };
			}
		});

		await page.goto('/profil');
		await page.getByRole('button', { name: 'Wyloguj się' }).click();

		await expect(page).toHaveURL(/\/logowanie/);
		expect(api.calls.map((c) => c.key)).toContain('POST /auth/logout');
	});
});

test.describe('the profile', () => {
	test('shows the nickname, the settings and the import link', async ({ page }) => {
		await mockApi(page, profileMock().handlers);

		await page.goto('/profil');

		await expect(page.getByRole('heading', { level: 1, name: 'Profil' })).toBeVisible();
		await expect(page.getByRole('textbox', { name: 'Pseudonim' })).toHaveValue('ania');
		await expect(page.getByRole('link', { name: 'Importuj przepis' })).toHaveAttribute(
			'href',
			'/przepisy?import=1'
		);
		await expect(page.getByRole('combobox', { name: /Lodówka wybierana/ })).toContainText(
			'Moja lodówka'
		);
	});

	test('changing the liquid units updates the example at once and persists', async ({ page }) => {
		const mock = profileMock();
		const api = await mockApi(page, mock.handlers);

		await page.goto('/profil');
		await expect(page.getByText('Przykład: „¾ szklanki mleka”.')).toBeVisible();
		const kitchen = page.getByRole('button', { name: 'Szklanki i łyżki' });
		const metric = page.getByRole('button', { name: 'Mililitry' });
		await expect(kitchen).toHaveAttribute('aria-pressed', 'true');

		await metric.click();

		await expect(page.getByText('Przykład: „190 ml mleka”.')).toBeVisible();
		await expect(metric).toHaveAttribute('aria-pressed', 'true');
		await expect.poll(() => api.calls.some((c) => c.key === 'PUT /me/settings')).toBe(true);
		expect(api.calls.find((c) => c.key === 'PUT /me/settings')?.json).toEqual({
			liquidUnits: 'metric',
			defaultFridgeId: fridgeId
		});
		expect(mock.state.me.settings.liquidUnits).toBe('metric');

		await page.reload();
		await expect(page.getByRole('button', { name: 'Mililitry' })).toHaveAttribute(
			'aria-pressed',
			'true'
		);
		await expect(page.getByText('Przykład: „190 ml mleka”.')).toBeVisible();
	});

	test('a refused settings change is rolled back', async ({ page }) => {
		const mock = profileMock();
		await mockApi(page, {
			...mock.handlers,
			'PUT /me/settings': problem(404, 'fridge.not_found')
		});

		await page.goto('/profil');
		await page.getByRole('button', { name: 'Mililitry' }).click();

		await expect(page.getByRole('button', { name: 'Szklanki i łyżki' })).toHaveAttribute(
			'aria-pressed',
			'true'
		);
		await expect(page.getByText('Przykład: „¾ szklanki mleka”.')).toBeVisible();
	});

	test('picks and clears the default fridge', async ({ page }) => {
		const mock = profileMock();
		const api = await mockApi(page, mock.handlers);

		await page.goto('/profil');
		const select = page.getByRole('combobox', { name: /Lodówka wybierana/ });
		await select.click();
		await page.getByRole('option', { name: 'Dom rodziców' }).click();
		await expect.poll(() => api.calls.filter((c) => c.key === 'PUT /me/settings').length).toBe(1);
		expect(mock.state.me.settings.defaultFridgeId).toBe(otherFridgeId);

		await select.click();
		await page.getByRole('option', { name: /^Brak/ }).click();
		await expect.poll(() => api.calls.filter((c) => c.key === 'PUT /me/settings').length).toBe(2);
		expect(api.calls.filter((c) => c.key === 'PUT /me/settings')[1]?.json).toEqual({
			liquidUnits: 'kitchen',
			defaultFridgeId: null
		});
	});

	test('changes the nickname, and keeping your own name is allowed', async ({ page }) => {
		const mock = profileMock();
		const api = await mockApi(page, mock.handlers);

		await page.goto('/profil');
		const save = page.getByRole('button', { name: 'Zapisz pseudonim' });
		await expect(save).toBeDisabled();

		await page.getByRole('textbox', { name: 'Pseudonim' }).fill('Ania');
		await expect(page.getByText('Ten pseudonim jest już zajęty.')).toBeHidden();
		await expect(save).toBeEnabled();

		await page.getByRole('textbox', { name: 'Pseudonim' }).fill('ania_k');
		await expect(save).toBeEnabled();
		await save.click();

		await expect(page.getByText('Zapisano pseudonim.')).toBeVisible();
		expect(api.calls.find((c) => c.key === 'PUT /me/nickname')?.json).toEqual({
			nickname: 'ania_k'
		});
	});
});

test.describe('the invite page', () => {
	const invitePreview = {
		fridgeName: 'Dom rodziców',
		owner: { id: '00000000-0000-7000-8000-000000000002', nickname: 'bartek' },
		expiresAt: '2026-11-01T10:00:00Z',
		alreadyMember: false
	};

	test('a logged-out visitor logs in, returns to the invite and joins', async ({ page }) => {
		const { handlers } = loginServer(me);
		const api = await mockApi(page, {
			...handlers,
			...fridgeHandlers(),
			'GET /invites/:token': { body: invitePreview },
			'POST /invites/:token/accept': { body: { ...summary, name: 'Dom rodziców' } }
		});

		await page.goto('/zaproszenie/tok123');
		await expect(page).toHaveURL(/\/logowanie\?next=%2Fzaproszenie%2Ftok123$/);
		await devLogin(page);

		await expect(page).toHaveURL(/\/zaproszenie\/tok123$/);
		await expect(page.getByText('Zaprasza: bartek')).toBeVisible();
		await page.getByRole('button', { name: 'Dołącz' }).click();

		await expect(page).toHaveURL(new RegExp(`/lodowka/${fridgeId}$`));
		expect(api.calls.map((c) => c.key)).toContain('POST /invites/tok123/accept');
	});

	test('a brand-new user picks a nickname and still lands on the invite', async ({ page }) => {
		const { handlers } = loginServer({ ...me, nickname: null });
		await mockApi(page, { ...handlers, 'GET /invites/:token': { body: invitePreview } });

		await page.goto('/zaproszenie/tok123');
		await devLogin(page);
		await expect(page).toHaveURL(/\/witaj\?next=%2Fzaproszenie%2Ftok123$/);
		await page.getByRole('textbox', { name: 'Pseudonim' }).fill('nowa_kasia');
		await page.getByRole('button', { name: 'Zacznij gotować' }).click();

		await expect(page).toHaveURL(/\/zaproszenie\/tok123$/);
		await expect(page.getByRole('button', { name: 'Dołącz' })).toBeVisible();
	});

	test('an expired invite shows a friendly empty state', async ({ page }) => {
		await mockApi(page, {
			...signedIn,
			'GET /invites/:token': problem(404, 'invite.not_found')
		});

		await page.goto('/zaproszenie/stary');

		await expect(page.getByRole('heading', { name: 'To zaproszenie wygasło' })).toBeVisible();
		await expect(page.getByRole('link', { name: 'Moje lodówki' })).toHaveAttribute(
			'href',
			'/lodowka'
		);
		await expect(page.getByRole('button', { name: 'Dołącz' })).toHaveCount(0);
	});
});

test('toasts on pages without the tab bar sit at the bottom edge', async ({ page }) => {
	const mock = profileMock({ ...me, nickname: null });
	await mockApi(page, mock.handlers);
	await page.goto('/witaj');

	const bottom = await page
		.getByRole('region', { name: 'Powiadomienia' })
		.evaluate((el) => window.innerHeight - el.getBoundingClientRect().bottom);

	// 0.75 rem of air, but none of the 4 rem tab bar that this page does not have.
	expect(bottom).toBeLessThan(40);
});

for (const colorScheme of ['light', 'dark'] as const) {
	test.describe(`accessibility, ${colorScheme} mode`, () => {
		test.use({ colorScheme, reducedMotion: 'reduce' });

		test('login, onboarding and profile have no violations', async ({ page }) => {
			test.slow();
			const { handlers } = loginServer({ ...me, nickname: null });
			await mockApi(page, handlers);

			await page.goto('/logowanie');
			await expect(page.getByRole('heading', { level: 1, name: 'Logowanie' })).toBeVisible();
			await expectNoViolations(page);

			await devLogin(page);
			await expect(page.getByRole('heading', { level: 1, name: 'Witaj!' })).toBeVisible();
			await page.getByRole('textbox', { name: 'Pseudonim' }).fill('bartek');
			await expect(page.getByText('Ten pseudonim jest już zajęty.')).toBeVisible();
			await expectNoViolations(page);

			const signed = profileMock();
			await page.unroute('**/api/v1/**');
			await mockApi(page, signed.handlers);
			await page.goto('/profil');
			await expect(page.getByRole('heading', { level: 1, name: 'Profil' })).toBeVisible();
			await expectNoViolations(page);
		});
	});
}
