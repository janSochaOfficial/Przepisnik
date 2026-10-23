<script lang="ts">
	import '../app.css';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { t } from '$lib/i18n';

	let { children } = $props();

	const links = [
		{ route: '/przepisy', label: t.common.nav.recipes },
		{ route: '/lodowka', label: t.common.nav.fridge },
		{ route: '/lista-zakupow', label: t.common.nav.shopping },
		{ route: '/profil', label: t.common.nav.profile }
	] as const;

	// The shell is hidden while signing in or picking a nickname. F0 replaces this simple nav.
	let showNav = $derived(!['/logowanie', '/witaj'].includes(page.url.pathname));
</script>

<svelte:head>
	<title>{t.common.appName}</title>
</svelte:head>

<div class="min-h-dvh pb-16 lg:pb-0 lg:pl-56">
	<main class="mx-auto max-w-3xl p-4">
		{@render children()}
	</main>

	{#if showNav}
		<nav
			aria-label={t.common.nav.label}
			class="border-border bg-surface fixed inset-x-0 bottom-0 border-t lg:inset-y-0 lg:right-auto lg:w-56 lg:border-t-0 lg:border-r"
		>
			<ul class="flex lg:flex-col lg:gap-1 lg:p-3">
				{#each links as link (link.route)}
					<li class="flex-1">
						<a
							href={resolve(link.route)}
							class="flex min-h-11 items-center justify-center px-2 text-sm lg:justify-start lg:rounded-md lg:px-3"
							aria-current={page.url.pathname.startsWith(link.route) ? 'page' : undefined}
						>
							{link.label}
						</a>
					</li>
				{/each}
			</ul>
		</nav>
	{/if}
</div>

<style>
	a[aria-current='page'] {
		color: var(--rl-accent);
		font-weight: 600;
	}
</style>
