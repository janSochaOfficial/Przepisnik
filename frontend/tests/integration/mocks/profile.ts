// Profile and onboarding fixtures (owned by F3). Typed with Schemas from ./types.
import { me } from './auth';
import { otherSummary, summary } from './fridge';
import type { MockHandlers } from './index';
import { problem } from './index';
import type { Schemas } from './types';

/** Nicknames that `GET /users/nickname-available` reports as taken (compared in lower case). */
export const takenNicknames = ['bartek', 'zosia'];

export type ProfileMock = {
	handlers: MockHandlers;
	/** The user as the "server" currently has them. */
	state: { me: Schemas['Me'] };
};

/** A stateful `/me`, `/me/settings` and `/me/nickname`, so tests can change settings and read them back. */
export function profileMock(initial: Schemas['Me'] = me): ProfileMock {
	const state = { me: structuredClone(initial) };
	const handlers: MockHandlers = {
		'GET /me': () => ({ body: state.me }),
		'GET /auth/config': { body: { googleClientId: null, devLoginEnabled: true } },
		'GET /fridges': { body: [summary, otherSummary] },
		'PUT /me/settings': (ctx) => {
			const body = ctx.json as Schemas['MeSettings'];
			state.me = { ...state.me, settings: { ...state.me.settings, ...body } };
			return { body: state.me };
		},
		'PUT /me/nickname': (ctx) => {
			const { nickname } = ctx.json as { nickname: string };
			if (takenNicknames.includes(nickname.toLowerCase())) return problem(409, 'nickname.taken');
			state.me = { ...state.me, nickname };
			return { body: state.me };
		},
		'GET /users/nickname-available': (ctx) => {
			const nickname = (ctx.url.searchParams.get('nickname') ?? '').toLowerCase();
			const own = state.me.nickname?.toLowerCase() === nickname;
			return { body: { available: own || !takenNicknames.includes(nickname) } };
		},
		'POST /auth/logout': () => ({ status: 204 })
	};
	return { handlers, state };
}

export const profileHandlers: MockHandlers = profileMock().handlers;
