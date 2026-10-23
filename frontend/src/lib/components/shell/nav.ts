import BookOpen from '@lucide/svelte/icons/book-open';
import CircleUserRound from '@lucide/svelte/icons/circle-user-round';
import Refrigerator from '@lucide/svelte/icons/refrigerator';
import ShoppingBasket from '@lucide/svelte/icons/shopping-basket';
import { t } from '$lib/i18n';
import type { IconComponent } from '$lib/components/ui/types';

/** The four destinations of PROJECT §3, in tab-bar order. */
export const NAV_ITEMS = [
	{ route: '/przepisy', label: t.common.nav.recipes, icon: BookOpen },
	{ route: '/lodowka', label: t.common.nav.fridge, icon: Refrigerator },
	{ route: '/lista-zakupow', label: t.common.nav.shopping, icon: ShoppingBasket },
	{ route: '/profil', label: t.common.nav.profile, icon: CircleUserRound }
] as const satisfies readonly { route: string; label: string; icon: IconComponent }[];

export type NavRoute = (typeof NAV_ITEMS)[number]['route'];

/** Routes where the shell (tab bar / rail) is hidden: signing in and onboarding. */
export const BARE_ROUTES: readonly string[] = ['/logowanie', '/witaj'];

/** The destination that owns `pathname` (e.g. `/przepisy/12/edytuj` → `/przepisy`), or null. */
export function activeRoute(pathname: string): NavRoute | null {
	// Variant editing (/warianty/…) belongs to the recipe catalog.
	if (pathname.startsWith('/warianty/')) return '/przepisy';
	const item = NAV_ITEMS.find((i) => pathname === i.route || pathname.startsWith(`${i.route}/`));
	return item?.route ?? null;
}

export function showsShell(pathname: string): boolean {
	return !BARE_ROUTES.includes(pathname);
}
