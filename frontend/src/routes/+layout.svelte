<script lang="ts">
	import '../app.css';
	import { page } from '$app/state';
	import { t } from '$lib/i18n';
	import AppShell from '$lib/components/shell/AppShell.svelte';
	import { showsShell } from '$lib/components/shell/nav';
	import Toaster from '$lib/components/ui/Toaster.svelte';
	import { provideSession } from '$lib/state/session.svelte';
	import type { LayoutProps } from './$types';

	let { data, children }: LayoutProps = $props();

	// svelte-ignore state_referenced_locally
	const session = provideSession(data.me);
	$effect.pre(() => session.sync(data.me));
</script>

<svelte:head>
	<title>{t.common.appName}</title>
</svelte:head>

<AppShell pathname={page.url.pathname}>
	{@render children()}
</AppShell>

<Toaster tabBar={showsShell(page.url.pathname)} />
