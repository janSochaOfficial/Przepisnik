<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { page } from '$app/state';
	import { createApi, unwrap } from '$lib/api/client';
	import { draftFromRecipe, type RecipeDraft } from '$lib/components/recipe-editor/draft';
	import VariantForm from '$lib/components/recipe-view/VariantForm.svelte';
	import { Page } from '$lib/components/shell';
	import { Button, EmptyState, toast } from '$lib/components/ui';
	import type { VariantWrite, Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	// Seeded once: from the current selection (navigation state) or from the base recipe itself.
	/* svelte-ignore state_referenced_locally */
	let draft = $state<RecipeDraft>(
		page.state.variantDraft ?? draftFromRecipe(data.recipe, { base: true })
	);
	let name = $state('');
	let description = $state('');
	let visibility = $state<Visibility>('private');
	let guard = $state(true);

	let recipe = $derived(data.recipe);
	let back = $derived(resolve('/przepisy/[id]', { id: recipe.id }));

	async function save(write: VariantWrite) {
		const created = await unwrap(
			createApi().POST('/recipes/{id}/variants', {
				params: { path: { id: recipe.id } },
				body: write
			})
		);
		toast.success(t.variants.editor.created);
		guard = false;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- a resolved path plus a query string
		await goto(`${back}?wariant=${created.id}`);
	}
</script>

<Page title={t.variants.newTitle} subtitle={t.variants.editor.basedOn(recipe.title)} {back}>
	{#if recipe.isArchived}
		<EmptyState title={t.variants.newTitle} description={t.variants.editor.archived}>
			{#snippet action()}
				<Button href={back}>{t.variants.editor.back}</Button>
			{/snippet}
		</EmptyState>
	{:else}
		<VariantForm
			base={recipe}
			bind:draft
			bind:name
			bind:description
			bind:visibility
			bind:guard
			onsave={save}
			cancelHref={back}
		/>
	{/if}
</Page>
