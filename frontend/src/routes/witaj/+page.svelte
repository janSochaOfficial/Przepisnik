<script lang="ts">
	import { goto } from '$app/navigation';
	import { page } from '$app/state';
	import { ApiError } from '$lib/api/client';
	import NicknameField from '$lib/components/auth/NicknameField.svelte';
	import { Page } from '$lib/components/shell';
	import { Button, ErrorMessage } from '$lib/components/ui';
	import { errorMessage, t } from '$lib/i18n';
	import { safeNext } from '$lib/state/next';
	import { useSession } from '$lib/state/session.svelte';
	import { cleanNickname } from '$lib/components/auth/nickname';

	const session = useSession();

	let nickname = $state('');
	let valid = $state(false);
	let takenError = $state<string | null>(null);
	let saving = $state(false);
	let error = $state<unknown>(null);

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		if (!valid || saving) return;
		saving = true;
		error = null;
		try {
			await session.setNickname(cleanNickname(nickname));
			// `next` is an in-app path validated by safeNext(), not a route id.
			// eslint-disable-next-line svelte/no-navigation-without-resolve
			await goto(safeNext(page.url.searchParams.get('next')), { invalidateAll: true });
		} catch (e) {
			if (e instanceof ApiError && (e.code === 'nickname.taken' || e.code === 'nickname.invalid')) {
				takenError = errorMessage(e.code);
			} else {
				error = e;
			}
		} finally {
			saving = false;
		}
	}
</script>

<Page title={t.auth.welcomeTitle}>
	<form class="flex max-w-md flex-col gap-5 pt-2" onsubmit={submit}>
		<p class="text-base text-muted">{t.auth.welcomeIntro}</p>
		<ErrorMessage {error} />
		<NicknameField
			bind:value={nickname}
			bind:valid
			error={takenError}
			onedit={() => (takenError = null)}
		/>
		<Button type="submit" size="lg" fullWidth loading={saving} disabled={!valid}>
			{t.auth.welcomeSubmit}
		</Button>
	</form>
</Page>
