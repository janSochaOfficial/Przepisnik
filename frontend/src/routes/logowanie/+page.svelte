<script lang="ts">
	import { goto } from '$app/navigation';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import { errorMessage, t } from '$lib/i18n';
	import type { PageProps } from './$types';

	// Minimal login page from T00: the dev-login form works end to end. F3 adds Google sign-in and the design.
	let { data }: PageProps = $props();

	let email = $state('');
	let nickname = $state('');
	let submitting = $state(false);
	let error = $state<string | null>(null);

	let showDevLogin = $derived(data.config.devLoginEnabled && !data.config.googleClientId);

	async function devLogin(event: SubmitEvent) {
		event.preventDefault();
		submitting = true;
		error = null;
		try {
			const api = createApi({ onUnauthorized: null });
			await unwrap(
				api.POST('/auth/dev-login', {
					body: { email: email.trim(), nickname: nickname.trim() === '' ? null : nickname.trim() }
				})
			);
			// `next` is an arbitrary in-app path from the query string (validated in +page.ts), not a route id.
			// eslint-disable-next-line svelte/no-navigation-without-resolve
			await goto(data.next, { invalidateAll: true });
		} catch (e) {
			error = errorMessage(e instanceof ApiError ? e.code : 'server.error');
		} finally {
			submitting = false;
		}
	}
</script>

<h1 class="text-2xl font-semibold">{t.auth.loginTitle}</h1>
<p class="text-muted mt-2">{t.auth.loginIntro}</p>

{#if showDevLogin}
	<form
		class="mt-6 flex max-w-sm flex-col gap-3"
		onsubmit={devLogin}
		aria-labelledby="dev-login-title"
	>
		<h2 id="dev-login-title" class="text-lg font-medium">{t.auth.devLogin.title}</h2>
		<p class="text-muted text-sm">{t.auth.devLogin.hint}</p>
		<label class="flex flex-col gap-1">
			<span>{t.auth.devLogin.email}</span>
			<input
				class="border-border min-h-11 rounded-md border px-3"
				type="email"
				name="email"
				autocomplete="email"
				required
				bind:value={email}
			/>
		</label>
		<label class="flex flex-col gap-1">
			<span>{t.auth.devLogin.nickname}</span>
			<input
				class="border-border min-h-11 rounded-md border px-3"
				type="text"
				name="nickname"
				autocomplete="nickname"
				maxlength="30"
				bind:value={nickname}
			/>
		</label>
		{#if error}
			<p role="alert" class="text-sm text-red-700 dark:text-red-400">{error}</p>
		{/if}
		<button
			type="submit"
			class="bg-accent min-h-11 rounded-md px-4 font-medium text-white disabled:opacity-60"
			disabled={submitting}
		>
			{t.auth.devLogin.submit}
		</button>
	</form>
{:else}
	<p class="mt-6">{t.auth.googleComingSoon}</p>
{/if}
