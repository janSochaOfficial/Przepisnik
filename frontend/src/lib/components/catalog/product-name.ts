/** The client-side twin of the server's name normalization: case, diacritics and spacing ignored. */
export function normalizeProductName(name: string): string {
	return name
		.normalize('NFD')
		.replace(/\p{M}/gu, '')
		.toLocaleLowerCase('pl')
		.replace(/\u0142/g, 'l')
		.trim()
		.replace(/\s+/g, ' ');
}

export function sameProductName(a: string, b: string): boolean {
	return normalizeProductName(a) === normalizeProductName(b);
}
