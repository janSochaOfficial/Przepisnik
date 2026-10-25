import { describe, expect, it } from 'vitest';
import { cookBlockedUntil, COOK_WINDOW_MS, formatRetryTime, isCookBlocked } from './cook';

describe('cookBlockedUntil', () => {
	it('is 12 hours after the last counted cook', () => {
		const until = cookBlockedUntil('2026-10-25T10:00:00Z');
		expect(until?.toISOString()).toBe('2026-10-25T22:00:00.000Z');
	});

	it('is null without a counted cook or with garbage', () => {
		expect(cookBlockedUntil(null)).toBeNull();
		expect(cookBlockedUntil(undefined)).toBeNull();
		expect(cookBlockedUntil('nonsense')).toBeNull();
	});
});

describe('isCookBlocked', () => {
	const last = '2026-10-25T10:00:00Z';
	const at = (ms: number) => new Date(Date.parse(last) + ms);

	it('blocks strictly before the 12 hours are over', () => {
		expect(isCookBlocked(last, at(0))).toBe(true);
		expect(isCookBlocked(last, at(COOK_WINDOW_MS - 1))).toBe(true);
	});

	it('allows exactly at and after 12 hours (the server counts at <= now - 12 h)', () => {
		expect(isCookBlocked(last, at(COOK_WINDOW_MS))).toBe(false);
		expect(isCookBlocked(last, at(COOK_WINDOW_MS + 1))).toBe(false);
	});

	it('never blocks without a counted cook', () => {
		expect(isCookBlocked(null, new Date())).toBe(false);
	});
});

describe('formatRetryTime', () => {
	it('shows only the time on the same day', () => {
		const now = new Date(2026, 9, 25, 9, 5);
		expect(formatRetryTime(new Date(2026, 9, 25, 21, 5), now)).toBe('21:05');
	});

	it('adds the date on another day', () => {
		const now = new Date(2026, 9, 25, 20, 0);
		expect(formatRetryTime(new Date(2026, 9, 26, 8, 0), now)).toBe('26 października, 08:00');
	});
});
