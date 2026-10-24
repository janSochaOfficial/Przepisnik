// The signed-in user (`GET /me`) and the calls that change it. The root layout creates one Session from
// its load data and provides it through context; pages read `me` and `settings` from it.
import { getContext, setContext } from 'svelte';
import { goto, invalidate } from '$app/navigation';
import { resolve } from '$app/paths';
import { createApi, unwrap } from '$lib/api/client';
import type { LiquidUnits, Me } from '$lib/domain/types';

type Settings = Me['settings'];

const DEFAULT_SETTINGS: Settings = { liquidUnits: 'kitchen', defaultFridgeId: null };

export class Session {
	me = $state.raw<Me | null>(null);
	settings = $derived<Settings>(this.me?.settings ?? DEFAULT_SETTINGS);
	liquidUnits = $derived<LiquidUnits>(this.settings.liquidUnits);

	constructor(me: Me | null = null) {
		this.me = me;
	}

	/** Takes over a fresh `Me` from the layout load. */
	sync(me: Me | null): void {
		this.me = me;
	}

	/** Re-reads `/me` and refreshes everything that depends on it (page data included). */
	async refresh(): Promise<void> {
		this.me = await unwrap(createApi().GET('/me'));
		await invalidate('app:me');
	}

	async logout(): Promise<void> {
		await unwrap(createApi({ onUnauthorized: null }).POST('/auth/logout'));
		this.me = null;
		await goto(resolve('/logowanie'), { invalidateAll: true });
	}

	/**
	 * Saves settings. The change shows at once and is rolled back when the server refuses it.
	 * `defaultFridgeId: null` clears the default.
	 */
	async updateSettings(patch: Partial<Settings>): Promise<void> {
		const before = this.me;
		if (!before) return;
		const next: Settings = { ...DEFAULT_SETTINGS, ...before.settings, ...patch };
		this.me = { ...before, settings: next };
		try {
			this.me = await unwrap(createApi().PUT('/me/settings', { body: next }));
		} catch (error) {
			this.me = before;
			throw error;
		}
		await invalidate('app:me');
	}

	/** Sets the nickname; the response re-issues the session, so no reload is needed (B1). */
	async setNickname(nickname: string): Promise<void> {
		this.me = await unwrap(createApi().PUT('/me/nickname', { body: { nickname } }));
		await invalidate('app:me');
	}
}

const KEY = Symbol('session');

export function provideSession(me: Me | null): Session {
	return setContext(KEY, new Session(me));
}

export function useSession(): Session {
	return getContext<Session>(KEY);
}
