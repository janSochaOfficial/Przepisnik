<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import { resolveNames } from '$lib/components/catalog/names';
	import {
		emptyDraft,
		mapServerErrors,
		toRecipeWrite,
		type FieldErrors
	} from '$lib/components/recipe-editor/draft';
	import RecipeEditor from '$lib/components/recipe-editor/RecipeEditor.svelte';
	import { Page } from '$lib/components/shell';
	import { toast } from '$lib/components/ui';
	import type { Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	let draft = $state(emptyDraft());
	let visibility = $state<Visibility>('private');
	let saving = $state(false);
	let error = $state<unknown>(null);
	let serverErrors = $state<FieldErrors>({});
	let guard = $state(true);

	async function save() {
		saving = true;
		error = null;
		serverErrors = {};
		let maps = toRecipeWrite(draft, { tagIds: [], utensilIds: [] }).maps;
		try {
			const [tagIds, utensilIds] = await Promise.all([
				resolveNames('tags', draft.tags),
				resolveNames('utensils', draft.utensils)
			]);
			const built = toRecipeWrite(draft, { tagIds, utensilIds });
			maps = built.maps;
			const created = await unwrap(
				createApi().POST('/recipes', { body: { ...built.write, visibility } })
			);
			toast.success(t.recipes.editor.created);
			guard = false;
			await goto(resolve('/przepisy/[id]', { id: created.id }));
		} catch (e) {
			error = e;
			if (e instanceof ApiError) serverErrors = mapServerErrors(e.errors, maps);
		} finally {
			saving = false;
		}
	}
</script>

<Page title={t.recipes.newTitle} back={resolve('/przepisy')}>
	<RecipeEditor
		bind:draft
		bind:visibility
		bind:guard
		{saving}
		{error}
		{serverErrors}
		onsubmit={save}
		cancelHref={resolve('/przepisy')}
	/>
</Page>
