/** The page after login or onboarding when the URL says nothing else. */
export const DEFAULT_NEXT = '/przepisy';

/** Only same-app paths are allowed as a post-login target (no open redirects). */
export function safeNext(next: string | null | undefined): string {
	return next && next.startsWith('/') && !next.startsWith('//') && !next.startsWith('/\\')
		? next
		: DEFAULT_NEXT;
}
