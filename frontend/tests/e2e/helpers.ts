// Helpers for the E2E smoke suite: everything goes through the real UI and the real API.
import { expect, type Locator, type Page } from '@playwright/test';
import { deflateSync } from 'node:zlib';

/** A suffix that keeps nicknames and titles unique when the stack's database is reused between runs. */
export const runId = Date.now().toString(36).slice(-6);

/** Signs in through the dev-login form (enabled by the Testing environment) with a nickname, so no onboarding. */
export async function devLogin(page: Page, nickname: string) {
	await page.goto('/logowanie');
	await page.getByLabel('E-mail').fill(`${nickname}@e2e.test`);
	await page.getByLabel('Pseudonim').fill(nickname);
	await page.getByRole('button', { name: 'Zaloguj się' }).click();
	await expect(page).toHaveURL(/\/przepisy$/);
}

/** Picks a catalog product in ingredient row `row` (1-based) of the recipe or variant editor. */
export async function pickProduct(page: Page, row: number, name: string): Promise<Locator> {
	const group = page.getByRole('listitem', { name: `Składnik ${row}` });
	await group.getByLabel('Produkt').fill(name);
	await page
		.getByRole('option', { name: new RegExp(`^${name}`) })
		.first()
		.click();
	await expect(group.getByLabel('Produkt')).toHaveValue(name);
	return group;
}

/** Chooses `option` in a bits-ui select labelled `label` inside `scope`. */
export async function choose(page: Page, scope: Locator, label: string, option: string) {
	await scope.getByLabel(label).click();
	await page.getByRole('option', { name: option, exact: true }).click();
}

/** Picks a visibility radio; "Publiczny" asks for a confirmation first. */
export async function chooseVisibility(
	page: Page,
	scope: Locator,
	label: 'Prywatny' | 'Publiczny'
) {
	await scope.getByRole('radio', { name: new RegExp(label) }).click({ force: true });
	const confirm = page.getByRole('alertdialog', { name: /publicznym/ });
	if (label === 'Publiczny') {
		// The recipe editor asks before going public; the variant form may not.
		await confirm
			.getByRole('button', { name: 'Zrób publicznym' })
			.click({ timeout: 3000 })
			.catch(() => undefined);
	}
	await expect(scope.getByRole('radio', { name: new RegExp(label) })).toBeChecked();
}

/** The id at the end of `/przepisy/<id>` (before any query). */
export function recipeIdFrom(url: string): string {
	const match = /\/przepisy\/([0-9a-f-]{36})/.exec(url);
	if (!match?.[1]) throw new Error(`no recipe id in ${url}`);
	return match[1];
}

/** A small valid PNG (solid colour) for photo uploads; the API re-encodes it to WebP. */
export function solidPng(width: number, height: number, rgb: [number, number, number]): Buffer {
	const crcTable = Array.from({ length: 256 }, (_, n) => {
		let c = n;
		for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
		return c >>> 0;
	});
	const crc = (buf: Buffer) => {
		let c = 0xffffffff;
		for (const byte of buf) c = (crcTable[(c ^ byte) & 0xff] ?? 0) ^ (c >>> 8);
		return (c ^ 0xffffffff) >>> 0;
	};
	const chunk = (type: string, data: Buffer) => {
		const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
		const out = Buffer.alloc(body.length + 8);
		out.writeUInt32BE(data.length, 0);
		body.copy(out, 4);
		out.writeUInt32BE(crc(body), body.length + 4);
		return out;
	};
	const header = Buffer.alloc(13);
	header.writeUInt32BE(width, 0);
	header.writeUInt32BE(height, 4);
	header[8] = 8; // bit depth
	header[9] = 2; // truecolour
	const row = Buffer.concat([Buffer.from([0]), Buffer.alloc(width * 3)]);
	for (let x = 0; x < width; x++) row.set(rgb, 1 + x * 3);
	const pixels = Buffer.concat(Array.from({ length: height }, () => row));
	return Buffer.concat([
		Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
		chunk('IHDR', header),
		chunk('IDAT', deflateSync(pixels)),
		chunk('IEND', Buffer.alloc(0))
	]);
}
