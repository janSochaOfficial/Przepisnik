// Auth fixtures (owned by F3).
import type { MockHandlers } from './index';
import type { Schemas } from './types';

export const me: Schemas['Me'] = {
	id: '00000000-0000-7000-8000-000000000001',
	email: 'ania@example.test',
	nickname: 'ania',
	avatarUrl: null,
	isAdmin: false,
	settings: { liquidUnits: 'kitchen', defaultFridgeId: '00000000-0000-7000-8000-0000000000f1' }
};

export const authConfig: Schemas['AuthConfig'] = { googleClientId: null, devLoginEnabled: true };

/** A signed-in, onboarded user. */
export const signedIn: MockHandlers = {
	'GET /me': { body: me },
	'GET /auth/config': { body: authConfig }
};
