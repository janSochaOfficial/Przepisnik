// Copy and Web Share helpers (invite links, shopping list).

/** Puts `text` on the clipboard; false when the browser refuses. */
export async function copyText(text: string): Promise<boolean> {
	try {
		await navigator.clipboard.writeText(text);
		return true;
	} catch {
		return false;
	}
}

/** Whether the system share sheet exists (phones, some desktops). */
export function canShare(): boolean {
	return typeof navigator !== 'undefined' && typeof navigator.share === 'function';
}

/** Opens the share sheet. Resolves false when the user dismisses it or sharing fails. */
export async function shareText(data: { title?: string; text?: string; url?: string }) {
	try {
		await navigator.share(data);
		return true;
	} catch {
		return false;
	}
}
