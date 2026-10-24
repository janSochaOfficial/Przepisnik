import { describe, expect, it } from 'vitest';
import { fileNameFrom } from './transfer';
import { checkImageFile } from './upload';

describe('fileNameFrom', () => {
	it('reads a quoted file name', () => {
		expect(fileNameFrom('attachment; filename="placki.json"', 'x.json')).toBe('placki.json');
	});

	it('reads an RFC 5987 file name', () => {
		expect(fileNameFrom("attachment; filename*=UTF-8''zurek%20na%20zakwasie.json", 'x.json')).toBe(
			'zurek na zakwasie.json'
		);
	});

	it('falls back without a header', () => {
		expect(fileNameFrom(null, 'x.json')).toBe('x.json');
	});
});

describe('checkImageFile', () => {
	it('accepts JPEG, PNG and WebP up to 10 MB', () => {
		expect(checkImageFile({ type: 'image/webp', size: 10 * 1024 * 1024 })).toBeNull();
		expect(checkImageFile({ type: 'image/png', size: 1 })).toBeNull();
	});

	it('rejects other types and big files', () => {
		expect(checkImageFile({ type: 'image/gif', size: 1 })).toBe('image.invalid_type');
		expect(checkImageFile({ type: 'image/jpeg', size: 10 * 1024 * 1024 + 1 })).toBe(
			'image.too_large'
		);
	});
});
