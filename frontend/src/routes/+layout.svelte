<script lang="ts">
	import '../app.css';
	import { page } from '$app/state';
	import { t } from '$lib/i18n';
	import AppShell from '$lib/components/shell/AppShell.svelte';
	import Toaster from '$lib/components/ui/Toaster.svelte';

	let { children } = $props();
</script>

<svelte:head>
	<title>{t.common.appName}</title>
</svelte:head>

<AppShell pathname={page.url.pathname}>
	{@render children()}
</AppShell>

<Toaster />
