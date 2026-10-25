// The 12-hour rule for cook counts (PROJECT §9.1, D-43). The server is the authority (409 `cook.too_soon`);
// the client derives the state from `RecipeDetail.myLastCountedCookAt` and its own clock.

/** One user's cooks of one recipe count at most once per this long. */
export const COOK_WINDOW_MS = 12 * 60 * 60 * 1000;

/** When the next cook counts: 12 hours after the last counted one; null when there was none. */
export function cookBlockedUntil(lastCountedAt: string | null | undefined): Date | null {
	if (!lastCountedAt) return null;
	const last = Date.parse(lastCountedAt);
	return Number.isNaN(last) ? null : new Date(last + COOK_WINDOW_MS);
}

/** True while `now` is before the end of the 12 hours after the last counted cook. */
export function isCookBlocked(lastCountedAt: string | null | undefined, now: Date): boolean {
	const until = cookBlockedUntil(lastCountedAt);
	return until !== null && now.getTime() < until.getTime();
}

const timeFormat = new Intl.DateTimeFormat('pl-PL', { hour: '2-digit', minute: '2-digit' });
const dateFormat = new Intl.DateTimeFormat('pl-PL', { day: 'numeric', month: 'long' });

function sameDay(a: Date, b: Date): boolean {
	return (
		a.getFullYear() === b.getFullYear() &&
		a.getMonth() === b.getMonth() &&
		a.getDate() === b.getDate()
	);
}

/** `07:30` today, `26 <month>, 07:30` on another day (local time, `pl-PL`). */
export function formatRetryTime(until: Date, now: Date): string {
	const time = timeFormat.format(until);
	return sameDay(until, now) ? time : `${dateFormat.format(until)}, ${time}`;
}
