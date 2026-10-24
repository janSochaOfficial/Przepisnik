<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { createApi, unwrap } from '$lib/api/client';
	import { Page } from '$lib/components/shell';
	import { Button, ErrorMessage, TextField } from '$lib/components/ui';
	import type { Me } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import type { PageProps } from './$types';
	import { renderGoogleButton } from './google';

	let { data }: PageProps = $props();

	let email = $state('');
	let nickname = $state('');
	let submitting = $state(false);
	let error = $state<unknown>(null);
	let googleButton = $state<HTMLElement>();
	let googleFailed = $state(false);

	let clientId = $derived(data.config.googleClientId ?? null);
	let showDevLogin = $derived(data.config.devLoginEnabled && !clientId);

	/** `/witaj` while the user has no nickname, then the page they asked for. */
	async function afterLogin(me: Me) {
		const target =
			me.nickname === null ? `/witaj?next=${encodeURIComponent(data.next)}` : data.next;
		// `next` is an in-app path validated by safeNext(), not a route id.
		// eslint-disable-next-line svelte/no-navigation-without-resolve
		await goto(target, { invalidateAll: true });
	}

	async function signIn(call: () => Promise<Me>) {
		submitting = true;
		error = null;
		try {
			await afterLogin(await call());
		} catch (e) {
			error = e;
		} finally {
			submitting = false;
		}
	}

	function devLogin(event: SubmitEvent) {
		event.preventDefault();
		void signIn(() =>
			unwrap(
				createApi({ onUnauthorized: null }).POST('/auth/dev-login', {
					body: { email: email.trim(), nickname: nickname.trim() === '' ? null : nickname.trim() }
				})
			)
		);
	}

	function googleLogin(idToken: string) {
		void signIn(() =>
			unwrap(createApi({ onUnauthorized: null }).POST('/auth/google', { body: { idToken } }))
		);
	}

	async function mountGoogle() {
		googleFailed = false;
		if (!clientId || !googleButton) return;
		try {
			await renderGoogleButton(googleButton, clientId, googleLogin);
		} catch {
			googleFailed = true;
		}
	}

	onMount(() => {
		if (clientId) void mountGoogle();
	});
</script>

<Page title={t.auth.loginTitle}>
	<div class="flex max-w-md flex-col gap-6 pt-2">
		<p class="text-base text-muted">{t.auth.loginIntro}</p>

		<ErrorMessage {error} />

		{#if clientId}
			<div class="flex flex-col items-start gap-3" aria-busy={submitting}>
				<div bind:this={googleButton} class="min-h-11 w-full"></div>
				{#if googleFailed}
					<p role="alert" class="text-sm font-semibold text-beet">{t.auth.googleFailed}</p>
					<Button variant="secondary" onclick={mountGoogle}>{t.auth.googleRetry}</Button>
				{/if}
			</div>
		{:else if showDevLogin}
			<form
				class="flex flex-col gap-5 rounded-card border border-line bg-surface p-5"
				onsubmit={devLogin}
				aria-labelledby="dev-login-title"
			>
				<div class="flex flex-col gap-1">
					<h2 id="dev-login-title" class="text-lg">{t.auth.devLogin.title}</h2>
					<p class="text-sm text-muted">{t.auth.devLogin.hint}</p>
				</div>
				<TextField
					label={t.auth.devLogin.email}
					type="email"
					name="email"
					autocomplete="email"
					bind:value={email}
				/>
				<TextField
					label={t.auth.devLogin.nickname}
					name="nickname"
					autocomplete="nickname"
					maxlength={30}
					optional
					bind:value={nickname}
				/>
				<Button type="submit" size="lg" fullWidth loading={submitting}>
					{t.auth.devLogin.submit}
				</Button>
			</form>
		{:else}
			<p class="text-base">{t.auth.notConfigured}</p>
		{/if}
	</div>
</Page>
