<script lang="ts">
	import { goto, invalidate } from '$app/navigation';
	import { resolve } from '$app/paths';
	import Eye from '@lucide/svelte/icons/eye';
	import Trash from '@lucide/svelte/icons/trash';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import ChangeVisibilityDialog from '$lib/components/catalog/ChangeVisibilityDialog.svelte';
	import { resolveNames } from '$lib/components/catalog/names';
	import DeleteRecipeDialog from '$lib/components/recipe-editor/DeleteRecipeDialog.svelte';
	import {
		draftFromRecipe,
		mapServerErrors,
		toRecipeWrite,
		type FieldErrors,
		type RowMaps
	} from '$lib/components/recipe-editor/draft';
	import RecipeEditor from '$lib/components/recipe-editor/RecipeEditor.svelte';
	import { Page } from '$lib/components/shell';
	import { Button, ConfirmDialog, EmptyState, toast } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import type { PageProps } from './$types';

	let { data }: PageProps = $props();

	// The form is seeded once from the loaded recipe; later edits belong to the user.
	// svelte-ignore state_referenced_locally
	let draft = $state(draftFromRecipe(data.recipe));
	let saving = $state(false);
	let error = $state<unknown>(null);
	let serverErrors = $state<FieldErrors>({});
	let guard = $state(true);
	let confirmVariants = $state(false);
	let variantCounts = $state({ total: 0, foreign: 0 });
	let deleting = $state(false);
	let visibilityOpen = $state(false);

	let recipe = $derived(data.recipe);
	let isAuthor = $derived(recipe.author.id === data.me?.id);
	let back = $derived(resolve('/przepisy/[id]', { id: recipe.id }));

	function numberOf(problem: unknown, key: string): number {
		const value = (problem as Record<string, unknown> | null)?.[key];
		return typeof value === 'number' ? value : 0;
	}

	/** Saves the form; `deleteVariants` is true only after the user confirmed (PROJECT §4.5). */
	async function save(deleteVariants = false) {
		saving = true;
		error = null;
		serverErrors = {};
		let maps: RowMaps = toRecipeWrite(draft, { tagIds: [], utensilIds: [] }).maps;
		try {
			const [tagIds, utensilIds] = await Promise.all([
				resolveNames('tags', draft.tags, recipe.tags),
				resolveNames('utensils', draft.utensils, recipe.utensils)
			]);
			const built = toRecipeWrite(draft, { tagIds, utensilIds });
			maps = built.maps;
			await unwrap(
				createApi().PUT('/recipes/{id}', {
					params: { path: { id: recipe.id } },
					body: { ...built.write, version: recipe.version, deleteVariants }
				})
			);
			toast.success(t.recipes.editor.saved);
			guard = false;
			await goto(resolve('/przepisy/[id]', { id: recipe.id }));
		} catch (e) {
			if (e instanceof ApiError && e.code === 'recipe.has_variants') {
				variantCounts = {
					total: numberOf(e.problem, 'variantCount'),
					foreign: numberOf(e.problem, 'foreignVariantCount')
				};
				confirmVariants = true;
			} else {
				error = e;
				if (e instanceof ApiError) serverErrors = mapServerErrors(e.errors, maps);
			}
		} finally {
			saving = false;
		}
	}
</script>

<Page title={t.recipes.editTitle} {back}>
	{#if !recipe.canEdit}
		{#if recipe.isArchived}
			<EmptyState
				title={t.recipes.readOnly.archivedTitle}
				description={t.recipes.readOnly.archivedText}
			>
				{#snippet action()}
					<Button href={back}>{t.recipes.readOnly.backToRecipe}</Button>
				{/snippet}
			</EmptyState>
		{:else if recipe.visibility === 'public'}
			<EmptyState
				title={t.recipes.readOnly.publicTitle}
				description={t.recipes.readOnly.publicText}
			>
				{#snippet action()}
					<Button href={resolve('/przepisy/[id]/warianty/nowy', { id: recipe.id })}>
						{t.recipes.readOnly.createVariant}
					</Button>
				{/snippet}
			</EmptyState>
		{:else}
			<EmptyState
				title={t.recipes.readOnly.notAuthorTitle}
				description={t.recipes.readOnly.notAuthorText}
			>
				{#snippet action()}
					<Button href={back}>{t.recipes.readOnly.backToRecipe}</Button>
				{/snippet}
			</EmptyState>
		{/if}
	{:else}
		<RecipeEditor
			bind:draft
			bind:guard
			{saving}
			{error}
			{serverErrors}
			onsubmit={() => save(false)}
			cancelHref={back}
		>
			{#snippet banner()}
				{#if recipe.totalVariantCount > 0}
					<p
						role="status"
						class="rounded-card border border-line-strong bg-cobalt-wash p-3 text-base"
					>
						{t.recipes.editor.variantsBanner(recipe.totalVariantCount, recipe.foreignVariantCount)}
					</p>
				{/if}
			{/snippet}
		</RecipeEditor>
		{#if isAuthor}
			<div class="mt-10">
				<Button variant="danger" icon={Trash} onclick={() => (deleting = true)}>
					{t.recipes.delete.action}
				</Button>
			</div>
			<DeleteRecipeDialog
				bind:open={deleting}
				recipeId={recipe.id}
				foreignVariantCount={recipe.foreignVariantCount}
				onDeleted={() => {
					guard = false;
					void goto(resolve('/przepisy'));
				}}
			/>
		{/if}
	{/if}
	{#if isAuthor && !recipe.isArchived}
		<div class="mt-4">
			<Button variant="secondary" icon={Eye} onclick={() => (visibilityOpen = true)}>
				{t.recipes.changeVisibility.title}
			</Button>
		</div>
		<ChangeVisibilityDialog
			bind:open={visibilityOpen}
			recipeId={recipe.id}
			visibility={recipe.visibility}
			onChanged={() => invalidate(`app:recipe:${recipe.id}`)}
		/>
	{/if}
</Page>

<ConfirmDialog
	bind:open={confirmVariants}
	title={t.recipes.editor.variantsTitle}
	description={t.recipes.editor.variantsConfirmText(variantCounts.total, variantCounts.foreign)}
	confirmLabel={t.recipes.editor.variantsConfirm}
	onConfirm={() => save(true)}
/>
