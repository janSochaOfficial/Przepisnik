<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import Trash from '@lucide/svelte/icons/trash';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import { emptyDraft, type RecipeDraft } from '$lib/components/recipe-editor/draft';
	import VariantForm from '$lib/components/recipe-view/VariantForm.svelte';
	import { variantDraft } from '$lib/components/recipe-view/variant-draft';
	import { Page } from '$lib/components/shell';
	import { Button, ConfirmDialog, EmptyState, toast } from '$lib/components/ui';
	import type { VariantWrite, Visibility } from '$lib/domain/types';
	import { errorMessage, t } from '$lib/i18n';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	const labels = t.variants.editor;

	// Everything below is seeded once from the loaded variant; edits belong to the user.
	/* svelte-ignore state_referenced_locally */
	let draft = $state<RecipeDraft>(
		data.base && data.resolved ? variantDraft(data.base, data.resolved) : emptyDraft()
	);
	/* svelte-ignore state_referenced_locally */
	let name = $state(data.variant.name);
	/* svelte-ignore state_referenced_locally */
	let description = $state(data.variant.description ?? '');
	/* svelte-ignore state_referenced_locally */
	let visibility = $state<Visibility>(data.variant.visibility);
	/* svelte-ignore state_referenced_locally */
	let version = $state(data.variant.version);
	let guard = $state(true);
	let deleting = $state(false);

	let variant = $derived(data.variant);
	let isAuthor = $derived(variant.author.id === data.me?.id);
	let back = $derived(
		data.base
			? resolve('/przepisy/[id]', { id: variant.recipeId }) + `?wariant=${variant.id}`
			: resolve('/przepisy')
	);

	async function save(write: VariantWrite) {
		try {
			await unwrap(
				createApi().PUT('/variants/{id}', {
					params: { path: { id: variant.id } },
					body: { ...write, version }
				})
			);
		} catch (error) {
			if (error instanceof ApiError && error.code === 'concurrency.conflict') {
				// Cooking bumps the version: take the fresh one, keep the draft and ask to save again.
				const fresh = await unwrap(
					createApi().GET('/variants/{id}', { params: { path: { id: variant.id } } })
				);
				version = fresh.version;
				toast.error(labels.conflict);
				return;
			}
			throw error;
		}
		toast.success(labels.saved);
		guard = false;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- a resolved path plus a query string
		await goto(back);
	}

	async function remove() {
		try {
			await unwrap(createApi().DELETE('/variants/{id}', { params: { path: { id: variant.id } } }));
		} catch (error) {
			toast.error(errorMessage(error instanceof ApiError ? error.code : 'server.error'));
			throw error;
		}
		toast.success(labels.deleted);
		guard = false;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- a resolved path plus a query string
		await goto(`${resolve('/przepisy')}?tab=variants`);
	}
</script>

<Page title={t.variants.editTitle} subtitle={variant.recipeTitle} {back}>
	{#if !isAuthor}
		<EmptyState title={t.variants.editTitle} description={labels.notAuthor}>
			{#snippet action()}
				<Button href={back}>{labels.back}</Button>
			{/snippet}
		</EmptyState>
	{:else if data.base}
		<VariantForm
			base={data.base}
			bind:draft
			bind:name
			bind:description
			bind:visibility
			bind:guard
			onsave={save}
			cancelHref={back}
		>
			<div class="mt-10">
				<Button variant="danger" icon={Trash} onclick={() => (deleting = true)}>
					{labels.delete}
				</Button>
			</div>
		</VariantForm>
	{:else}
		<EmptyState title={variant.name} description={labels.baseUnavailable}>
			{#snippet action()}
				<Button variant="danger" icon={Trash} onclick={() => (deleting = true)}>
					{labels.delete}
				</Button>
			{/snippet}
		</EmptyState>
	{/if}
</Page>

<ConfirmDialog
	bind:open={deleting}
	title={labels.deleteTitle}
	description={labels.deleteText(variant.name)}
	confirmLabel={labels.delete}
	onConfirm={remove}
/>
