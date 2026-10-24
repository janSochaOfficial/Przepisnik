<script lang="ts">
	import FileInput from '@lucide/svelte/icons/file-input';
	import LogOut from '@lucide/svelte/icons/log-out';
	import { invalidate } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { ApiError } from '$lib/api/client';
	import NicknameField from '$lib/components/auth/NicknameField.svelte';
	import { cleanNickname } from '$lib/components/auth/nickname';
	import { liquidExample } from '$lib/components/auth/units-example';
	import { Page } from '$lib/components/shell';
	import { Avatar, Button, Chip, ErrorMessage, Select, toast } from '$lib/components/ui';
	import type { LiquidUnits } from '$lib/domain/types';
	import { errorMessage, t } from '$lib/i18n';
	import { useSession } from '$lib/state/session.svelte';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();
	const session = useSession();
	const labels = t.profile;

	// --- nickname
	let nickname = $state(session.me?.nickname ?? '');
	let nicknameValid = $state(false);
	let nicknameError = $state<string | null>(null);
	let savingNickname = $state(false);
	let current = $derived(session.me?.nickname ?? null);
	let unchanged = $derived(current !== null && cleanNickname(nickname) === current);

	async function saveNickname(event: SubmitEvent) {
		event.preventDefault();
		if (!nicknameValid || savingNickname) return;
		savingNickname = true;
		try {
			await session.setNickname(cleanNickname(nickname));
			nickname = session.me?.nickname ?? nickname;
			toast.success(labels.nickname.saved);
		} catch (e) {
			if (e instanceof ApiError && (e.code === 'nickname.taken' || e.code === 'nickname.invalid')) {
				nicknameError = errorMessage(e.code);
			} else {
				toast.error(errorMessage(e instanceof ApiError ? e.code : 'server.error'));
			}
		} finally {
			savingNickname = false;
		}
	}

	// --- settings
	const NO_FRIDGE = 'none';
	const unitChoices: { value: LiquidUnits; label: string }[] = [
		{ value: 'kitchen', label: labels.units.kitchen },
		{ value: 'metric', label: labels.units.metric }
	];

	async function saveSettings(patch: Parameters<typeof session.updateSettings>[0]) {
		try {
			await session.updateSettings(patch);
			toast.success(labels.units.saved);
		} catch (e) {
			toast.error(errorMessage(e instanceof ApiError ? e.code : 'server.error'));
		}
	}

	let fridgeItems = $derived([
		{ value: NO_FRIDGE, label: labels.fridge.none },
		...(data.fridges ?? []).map((f) => ({ value: f.id, label: f.name }))
	]);
	let defaultFridge = $derived(session.settings.defaultFridgeId ?? NO_FRIDGE);

	let loggingOut = $state(false);
	async function logout() {
		loggingOut = true;
		try {
			await session.logout();
		} catch (e) {
			toast.error(errorMessage(e instanceof ApiError ? e.code : 'server.error'));
		} finally {
			loggingOut = false;
		}
	}
</script>

<Page title={labels.title}>
	<div class="flex flex-col gap-10 pt-2">
		<div class="flex items-center gap-4">
			<Avatar
				name={session.me?.nickname ?? ''}
				src={session.me?.avatarUrl ?? undefined}
				size="lg"
				decorative
			/>
			<div class="min-w-0">
				<p class="truncate text-lg font-semibold">{session.me?.nickname}</p>
				<p class="truncate text-sm text-muted">{session.me?.email}</p>
			</div>
		</div>

		<section class="flex flex-col gap-4" aria-labelledby="nickname-title">
			<h2 id="nickname-title" class="text-xl">{labels.nickname.title}</h2>
			<form class="flex flex-col items-start gap-4" onsubmit={saveNickname}>
				<NicknameField
					bind:value={nickname}
					bind:valid={nicknameValid}
					{current}
					error={nicknameError}
					onedit={() => (nicknameError = null)}
				/>
				<Button type="submit" loading={savingNickname} disabled={!nicknameValid || unchanged}>
					{labels.nickname.save}
				</Button>
			</form>
		</section>

		<section class="flex flex-col gap-4" aria-labelledby="units-title">
			<h2 id="units-title" class="text-xl">{labels.units.title}</h2>
			<div role="group" aria-label={labels.units.group} class="flex flex-wrap gap-2">
				{#each unitChoices as choice (choice.value)}
					<Chip
						selected={session.liquidUnits === choice.value}
						onclick={() =>
							session.liquidUnits !== choice.value && saveSettings({ liquidUnits: choice.value })}
					>
						{choice.label}
					</Chip>
				{/each}
			</div>
			<p class="text-base text-muted" aria-live="polite">
				{labels.units.example(liquidExample(session.liquidUnits))}
			</p>
		</section>

		<section class="flex flex-col gap-4" aria-labelledby="fridge-title">
			<h2 id="fridge-title" class="text-xl">{labels.fridge.title}</h2>
			{#if data.fridges}
				<Select
					label={labels.fridge.label}
					hint={labels.fridge.hint}
					items={fridgeItems}
					value={defaultFridge}
					onValueChange={(value) =>
						saveSettings({ defaultFridgeId: value === NO_FRIDGE ? null : value })}
				/>
			{:else}
				<ErrorMessage
					error={data.fridgesError}
					variant="block"
					retry={() => invalidate('app:profile')}
				/>
			{/if}
		</section>

		<section class="flex flex-col items-start gap-2">
			<Button variant="secondary" icon={FileInput} href={resolve('/przepisy') + '?import=1'}>
				{labels.importRecipe}
			</Button>
			<p class="text-sm text-muted">{labels.importHint}</p>
		</section>

		<section>
			<Button variant="ghost" icon={LogOut} loading={loggingOut} onclick={logout}>
				{labels.logout}
			</Button>
		</section>
	</div>
</Page>
