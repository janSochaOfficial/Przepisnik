<script lang="ts">
	import Copy from '@lucide/svelte/icons/copy';
	import Share2 from '@lucide/svelte/icons/share-2';
	import UserMinus from '@lucide/svelte/icons/user-minus';
	import { untrack } from 'svelte';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import {
		Avatar,
		Button,
		Chip,
		ConfirmDialog,
		Dialog,
		ErrorMessage,
		IconButton,
		TextField,
		toast
	} from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { canShare, copyText, shareText } from './clipboard';
	import type { FridgeDetail, FridgeSummary, Invite } from './types';

	/**
	 * Rename, members, invites and delete (PROJECT §8.1). The owner sees everything; a member sees
	 * the members list and "Opuść lodówkę". Mount it only while open.
	 */
	type Props = {
		open?: boolean;
		fridge: FridgeDetail;
		meId: string;
		onRenamed: (fridge: FridgeSummary) => void;
		/** Members changed (someone was removed): reload the detail. */
		onMembersChanged: () => void;
		/** The fridge is gone for this user (deleted, or they left). */
		onGone: () => void;
	};

	let {
		open = $bindable(false),
		fridge,
		meId,
		onRenamed,
		onMembersChanged,
		onGone
	}: Props = $props();

	const labels = t.fridge.settings;
	const isOwner = untrack(() => fridge.role === 'owner');
	const shareable = canShare();

	let name = $state(untrack(() => fridge.name));
	let renaming = $state(false);
	let invites = $state<Invite[]>([]);
	let creatingInvite = $state(false);
	let error = $state<unknown>(null);
	let confirmDelete = $state(false);
	let confirmLeave = $state(false);

	const dateFormat = new Intl.DateTimeFormat('pl-PL', { dateStyle: 'short' });

	function fail(e: unknown) {
		error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
	}

	$effect(() => {
		if (!isOwner) return;
		untrack(() => {
			unwrap(
				createApi().GET('/fridges/{id}/invites', { params: { path: { id: fridge.id } } })
			).then((list) => (invites = list), fail);
		});
	});

	async function rename(event: SubmitEvent) {
		event.preventDefault();
		if (!name.trim() || name.trim() === fridge.name) return;
		renaming = true;
		error = null;
		try {
			const updated = await unwrap(
				createApi().PUT('/fridges/{id}', {
					params: { path: { id: fridge.id } },
					body: { name: name.trim() }
				})
			);
			toast.success(labels.renamed);
			onRenamed(updated);
		} catch (e) {
			fail(e);
		} finally {
			renaming = false;
		}
	}

	async function removeMember(userId: string, nickname: string) {
		error = null;
		try {
			await unwrap(
				createApi().DELETE('/fridges/{id}/members/{userId}', {
					params: { path: { id: fridge.id, userId } }
				})
			);
			toast.success(labels.memberRemoved(nickname));
			onMembersChanged();
		} catch (e) {
			fail(e);
		}
	}

	async function leave() {
		try {
			await unwrap(
				createApi().DELETE('/fridges/{id}/members/{userId}', {
					params: { path: { id: fridge.id, userId: meId } }
				})
			);
		} catch (e) {
			fail(e);
			throw e;
		}
		open = false;
		toast.success(labels.left);
		onGone();
	}

	async function deleteFridge() {
		try {
			await unwrap(createApi().DELETE('/fridges/{id}', { params: { path: { id: fridge.id } } }));
		} catch (e) {
			fail(e);
			throw e;
		}
		open = false;
		toast.success(labels.deleted);
		onGone();
	}

	async function createInvite() {
		creatingInvite = true;
		error = null;
		try {
			const invite = await unwrap(
				createApi().POST('/fridges/{id}/invites', { params: { path: { id: fridge.id } } })
			);
			invites = [invite, ...invites];
			await copyInvite(invite);
		} catch (e) {
			fail(e);
		} finally {
			creatingInvite = false;
		}
	}

	const absolute = (invite: Invite) => new URL(invite.url, window.location.origin).href;

	async function copyInvite(invite: Invite) {
		if (await copyText(absolute(invite))) toast.success(labels.linkCopied);
		else toast.error(labels.copyFailed);
	}

	async function shareInvite(invite: Invite) {
		await shareText({ text: labels.shareText(fridge.name), url: absolute(invite) });
	}

	async function revoke(invite: Invite) {
		error = null;
		try {
			await unwrap(
				createApi().DELETE('/fridges/{id}/invites/{inviteId}', {
					params: { path: { id: fridge.id, inviteId: invite.id } }
				})
			);
			invites = invites.filter((i) => i.id !== invite.id);
			toast.success(labels.revoked);
		} catch (e) {
			fail(e);
		}
	}
</script>

<Dialog bind:open title={labels.title}>
	<div class="flex flex-col gap-8">
		<ErrorMessage {error} />

		{#if isOwner}
			<form class="flex flex-col gap-3" onsubmit={rename}>
				<TextField label={labels.nameLabel} bind:value={name} maxlength={80} />
				<Button
					type="submit"
					variant="secondary"
					loading={renaming}
					disabled={!name.trim() || name.trim() === fridge.name}
					class="self-start"
				>
					{labels.rename}
				</Button>
			</form>
		{/if}

		<section aria-labelledby="members-heading" class="flex flex-col gap-2">
			<h3 id="members-heading" class="font-serif text-lg font-semibold">{labels.members}</h3>
			<ul class="flex flex-col rounded-card border border-line">
				{#each fridge.members as member (member.id)}
					<li class="flex min-h-14 items-center gap-3 border-b border-line px-3 last:border-b-0">
						<Avatar name={member.nickname} decorative />
						<span class="flex-1 text-base">
							{member.nickname}
							{#if member.id === meId}<span class="text-sm text-muted">({labels.you})</span>{/if}
						</span>
						{#if member.role === 'owner'}
							<Chip>{labels.owner}</Chip>
						{:else if isOwner}
							<IconButton
								icon={UserMinus}
								label={labels.removeMember(member.nickname)}
								onclick={() => removeMember(member.id, member.nickname)}
							/>
						{/if}
					</li>
				{/each}
			</ul>
			{#if !isOwner}
				<Button variant="secondary" class="self-start" onclick={() => (confirmLeave = true)}>
					{labels.leave}
				</Button>
			{/if}
		</section>

		{#if isOwner}
			<section aria-labelledby="invites-heading" class="flex flex-col gap-2">
				<h3 id="invites-heading" class="font-serif text-lg font-semibold">{labels.invites}</h3>
				<p class="text-sm text-muted">{labels.invitesHint}</p>
				<Button
					variant="secondary"
					icon={Copy}
					class="self-start"
					loading={creatingInvite}
					onclick={createInvite}
				>
					{labels.createInvite}
				</Button>
				{#if invites.length === 0}
					<p class="text-sm text-muted">{labels.noInvites}</p>
				{:else}
					<ul class="flex flex-col rounded-card border border-line">
						{#each invites as invite (invite.id)}
							<li
								class="flex flex-wrap items-center gap-2 border-b border-line p-3 last:border-b-0"
							>
								<span class="flex-1 text-sm">
									{labels.inviteExpires(dateFormat.format(new Date(invite.expiresAt)))}
								</span>
								<IconButton
									icon={Copy}
									label={labels.copyLink}
									onclick={() => copyInvite(invite)}
								/>
								{#if shareable}
									<IconButton
										icon={Share2}
										label={labels.shareLink}
										onclick={() => shareInvite(invite)}
									/>
								{/if}
								<Button variant="ghost" onclick={() => revoke(invite)}>{labels.revoke}</Button>
							</li>
						{/each}
					</ul>
				{/if}
			</section>

			<Button variant="danger" class="self-start" onclick={() => (confirmDelete = true)}>
				{labels.deleteFridge}
			</Button>
		{/if}
	</div>
</Dialog>

<ConfirmDialog
	bind:open={confirmDelete}
	title={labels.deleteTitle}
	description={labels.deleteText}
	confirmLabel={labels.deleteFridge}
	onConfirm={deleteFridge}
/>
<ConfirmDialog
	bind:open={confirmLeave}
	title={labels.leaveTitle}
	description={labels.leaveText}
	confirmLabel={labels.leave}
	onConfirm={leave}
/>
