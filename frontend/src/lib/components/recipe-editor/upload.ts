import { ApiError, CSRF_HEADER, NETWORK_ERROR_CODE, toApiError } from '$lib/api/client';
import type { components } from '$lib/api/schema';

export type ImageInfo = components['schemas']['ImageInfo'];

export const MAX_IMAGE_BYTES = 10 * 1024 * 1024;
export const IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp'] as const;

/** Client-side check that mirrors the server limits (PROJECT §11); returns an API error code or null. */
export function checkImageFile(file: { type: string; size: number }): string | null {
	if (!(IMAGE_TYPES as readonly string[]).includes(file.type)) return 'image.invalid_type';
	if (file.size > MAX_IMAGE_BYTES) return 'image.too_large';
	return null;
}

/**
 * `POST /images` as multipart. Uses XMLHttpRequest because `fetch` cannot report upload progress.
 * Rejects with an {@link ApiError}.
 */
export function uploadImage(
	file: File,
	onProgress?: (percent: number) => void
): Promise<ImageInfo> {
	const invalid = checkImageFile(file);
	if (invalid) {
		return Promise.reject(new ApiError(invalid === 'image.too_large' ? 413 : 400, invalid));
	}
	return new Promise((resolve, reject) => {
		const xhr = new XMLHttpRequest();
		xhr.open('POST', '/api/v1/images');
		for (const [name, value] of Object.entries(CSRF_HEADER)) xhr.setRequestHeader(name, value);
		xhr.upload.onprogress = (event) => {
			if (event.lengthComputable) onProgress?.(Math.round((event.loaded / event.total) * 100));
		};
		xhr.onerror = () => reject(new ApiError(0, NETWORK_ERROR_CODE));
		xhr.onload = () => {
			let body: unknown;
			try {
				body = JSON.parse(xhr.responseText);
			} catch {
				body = undefined;
			}
			if (xhr.status >= 200 && xhr.status < 300) resolve(body as ImageInfo);
			else reject(toApiError(xhr.status, body));
		};
		const form = new FormData();
		form.append('file', file);
		xhr.send(form);
	});
}
