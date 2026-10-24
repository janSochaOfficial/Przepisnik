// Client-side mirror of backend `NicknameRules` (PROJECT §2, D-29): trimmed, NFC, 3–30 UTF-16 units of
// letters, digits, `_` or `-`. Uniqueness is the server's call (`GET /users/nickname-available`).
export const NICKNAME_MIN = 3;
export const NICKNAME_MAX = 30;

export type NicknameProblem = 'empty' | 'tooShort' | 'tooLong' | 'badChars';

const ALLOWED = /^[\p{L}\p{Nd}_-]+$/u;

/** The form the server stores and validates: trimmed and NFC-composed. */
export function cleanNickname(value: string): string {
	return value.trim().normalize('NFC');
}

export function nicknameProblem(value: string): NicknameProblem | null {
	const clean = cleanNickname(value);
	if (clean.length === 0) return 'empty';
	if (!ALLOWED.test(clean)) return 'badChars';
	if (clean.length < NICKNAME_MIN) return 'tooShort';
	if (clean.length > NICKNAME_MAX) return 'tooLong';
	return null;
}

/** Whether two nicknames differ only by case; the server says "available" for your own name anyway. */
export function sameNickname(a: string, b: string): boolean {
	return cleanNickname(a).toLowerCase() === cleanNickname(b).toLowerCase();
}
