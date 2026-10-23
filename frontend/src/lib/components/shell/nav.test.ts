import { describe, expect, it } from 'vitest';
import { activeRoute, NAV_ITEMS, showsShell } from './nav';

describe('activeRoute', () => {
	it.each([
		['/przepisy', '/przepisy'],
		['/przepisy/12/edytuj', '/przepisy'],
		['/warianty/3/edytuj', '/przepisy'],
		['/lodowka/7', '/lodowka'],
		['/lista-zakupow', '/lista-zakupow'],
		['/profil', '/profil']
	])('maps %s to %s', (path, route) => {
		expect(activeRoute(path)).toBe(route);
	});

	it('does not match a route that only shares a prefix', () => {
		expect(activeRoute('/przepisyx')).toBeNull();
		expect(activeRoute('/ui')).toBeNull();
	});
});

describe('showsShell', () => {
	it('hides the shell while signing in and onboarding', () => {
		expect(showsShell('/logowanie')).toBe(false);
		expect(showsShell('/witaj')).toBe(false);
		expect(showsShell('/przepisy')).toBe(true);
	});
});

it('lists the four destinations of PROJECT §3 in order', () => {
	expect(NAV_ITEMS.map((i) => i.label)).toEqual(['Przepisy', 'Lodówka', 'Lista zakupów', 'Profil']);
});
