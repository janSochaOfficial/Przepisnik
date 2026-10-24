<script lang="ts">
	import { onMount } from 'svelte';
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import { Page } from '$lib/components/shell';
	import { Button, ErrorMessage, Skeleton, toast } from '$lib/components/ui';
	import { t } from '$lib/i18n';

	const labels = t.fridge.join;

	type Preview = Awaited<ReturnType<typeof loadPreview>>;

	let token = $derived(page.params.token ?? '');
	let preview = $state<Preview | null>(null);
	let error = $state<unknown>(null);
	let joining = $state(false);

	function loadPreview(value: string) {
		return unwrap(createApi().GET('/invites/{token}', { params: { path: { token: value } } }));
	}

	async function load() {
		error = null;
		try {
			preview = await loadPreview(token);
		} catch (e) {
			error = e;
		}
	}

	onMount(load);

	async function accept() {
		joining = true;
		error = null;
		try {
			const fridge = await unwrap(
				createApi().POST('/invites/{token}/accept', { params: { path: { token } } })
			);
			if (!preview?.alreadyMember) toast.success(labels.joined);
			await goto(resolve('/lodowka/[id]', { id: fridge.id }));
		} catch (e) {
			error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			joining = false;
		}
	}

	const dateFormat = new Intl.DateTimeFormat('pl-PL', { dateStyle: 'short' });
</script>

<Page title={t.fridge.inviteTitle}>
	{#if error && !preview}
		<ErrorMessage {error} variant="block" retry={load} />
	{:else if !preview}
		<div class="flex flex-col gap-3" aria-busy="true">
			<Skeleton class="h-8 w-3/4" />
			<Skeleton class="h-5 w-1/2" />
		</div>
	{:else}
		<div class="flex flex-col gap-4">
			<ErrorMessage {error} />
			<h2 class="text-2xl">{labels.heading(preview.fridgeName)}</h2>
			<p class="text-base">{labels.from(preview.owner.nickname)}</p>
			<p class="text-sm text-muted">
				{labels.expires(dateFormat.format(new Date(preview.expiresAt)))}
			</p>
			{#if preview.alreadyMember}
				<p class="text-base">{labels.alreadyMember}</p>
			{/if}
			<Button size="lg" class="self-start" loading={joining} onclick={accept}>
				{preview.alreadyMember ? labels.open : labels.accept}
			</Button>
		</div>
	{/if}
</Page>
