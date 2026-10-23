<script lang="ts">
	import type { Snippet } from 'svelte';
	import { resolve } from '$app/paths';
	import { t } from '$lib/i18n';
	import Oczko from '$lib/components/ui/Oczko.svelte';
	import { activeRoute, NAV_ITEMS, showsShell } from './nav';

	/**
	 * The frame around every signed-in page: a bottom tab bar below 1024 px, a side rail from
	 * 1024 px (one `<nav>` that changes shape). `main` reserves room for the tab bar and the safe
	 * areas, so pages never pad for it themselves. Signing in and onboarding get the bare frame.
	 */
	let { pathname, children }: { pathname: string; children: Snippet } = $props();

	let active = $derived(activeRoute(pathname));
	let withNav = $derived(showsShell(pathname));
</script>

<a
	href="#main"
	class="sr-only z-70 rounded-control bg-cobalt px-4 py-3 font-semibold text-on-cobalt focus:not-sr-only focus:fixed focus:top-2 focus:left-2"
>
	{t.common.shell.skipToContent}
</a>

<div
	class={[
		'min-h-dvh pr-(--rl-safe-right) pl-(--rl-safe-left)',
		withNav && 'pb-[calc(var(--rl-tabbar-h)+var(--rl-safe-bottom))] lg:pb-0 lg:pl-(--rl-rail-w)'
	]}
>
	<main id="main" tabindex="-1" class="outline-none">
		{@render children()}
	</main>

	{#if withNav}
		<nav
			aria-label={t.common.nav.label}
			class="fixed inset-x-0 bottom-0 z-30 border-t border-line bg-surface pb-(--rl-safe-bottom) lg:inset-y-0 lg:right-auto lg:flex lg:w-(--rl-rail-w) lg:flex-col lg:border-t-0 lg:border-r lg:bg-paper lg:pt-6 lg:pb-6"
		>
			<a
				href={resolve('/przepisy')}
				class="mx-4 mb-8 hidden items-center gap-3 rounded-control px-2 py-1 lg:flex"
			>
				<Oczko size={40} />
				<span class="flex flex-col">
					<span class="font-serif text-xl leading-tight font-semibold">{t.common.appName}</span>
					<span class="text-xs text-muted">{t.common.shell.tagline}</span>
				</span>
			</a>
			<ul class="flex h-(--rl-tabbar-h) items-stretch lg:h-auto lg:flex-col lg:gap-1 lg:px-3">
				{#each NAV_ITEMS as item (item.route)}
					{@const current = active === item.route}
					<li class="flex-1 lg:flex-none">
						<a
							href={resolve(item.route)}
							aria-current={current ? 'page' : undefined}
							class={[
								'group flex h-full flex-col items-center justify-center gap-0.5 px-1 text-xs font-semibold transition-colors duration-150',
								'lg:min-h-12 lg:flex-row lg:justify-start lg:gap-3 lg:rounded-control lg:px-3 lg:text-base',
								current
									? 'text-ink lg:bg-cobalt-wash'
									: 'text-muted hover:text-ink lg:hover:bg-sunken'
							]}
						>
							<span
								class={[
									'flex h-8 w-14 items-center justify-center rounded-full transition-colors duration-150 lg:h-auto lg:w-auto',
									current ? 'bg-cobalt-wash text-cobalt lg:bg-transparent' : ''
								]}
							>
								<item.icon size={24} aria-hidden="true" strokeWidth={current ? 2.25 : 1.75} />
							</span>
							<span class="max-w-full truncate">{item.label}</span>
						</a>
					</li>
				{/each}
			</ul>
		</nav>
	{/if}
</div>
