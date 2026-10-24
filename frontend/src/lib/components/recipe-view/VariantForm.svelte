<script lang="ts">
	import type { Snippet } from 'svelte';
	import { ApiError } from '$lib/api/client';
	import VisibilitySelect from '$lib/components/catalog/VisibilitySelect.svelte';
	import { resolveNames } from '$lib/components/catalog/names';
	import {
		mapServerErrors,
		toVariantRecipeWrite,
		type FieldErrors,
		type RecipeDraft,
		type RowMaps
	} from '$lib/components/recipe-editor/draft';
	import RecipeEditor from '$lib/components/recipe-editor/RecipeEditor.svelte';
	import { TextArea, TextField } from '$lib/components/ui';
	import type { RecipeDetail, VariantWrite, Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	/**
	 * The variant editor: name, description and visibility above F4's `RecipeEditor` in `variant` mode.
	 * The page owns the request (`onsave`); thrown errors are shown here, mapped onto the rows.
	 */
	type Props = {
		/** The base recipe (its utensils are the known names; its visibility drives the hint). */
		base: Pick<RecipeDetail, 'visibility' | 'utensils'>;
		draft: RecipeDraft;
		name?: string;
		description?: string;
		visibility?: Visibility;
		onsave: (write: VariantWrite) => Promise<void>;
		cancelHref: string;
		guard?: boolean;
		children?: Snippet;
	};

	let {
		base,
		draft = $bindable(),
		name = $bindable(''),
		description = $bindable(''),
		visibility = $bindable('private'),
		onsave,
		cancelHref,
		guard = $bindable(true),
		children
	}: Props = $props();

	const labels = t.variants.editor;
	const rank: Record<Visibility, number> = { private: 0, fridges: 1, public: 2 };

	let saving = $state(false);
	let error = $state<unknown>(null);
	let serverErrors = $state<FieldErrors>({});
	let submitted = $state(false);

	let nameError = $derived.by(() => {
		if (!submitted) return null;
		if (name.trim() === '') return labels.nameRequired;
		return name.trim().length > 80 ? labels.nameTooLong : null;
	});
	let hint = $derived(rank[base.visibility] < rank[visibility] ? labels.narrowerHint : undefined);

	async function submit(current: RecipeDraft) {
		submitted = true;
		if (name.trim() === '' || name.trim().length > 80) return;
		saving = true;
		error = null;
		serverErrors = {};
		let maps: RowMaps = toVariantRecipeWrite(current, []).maps;
		try {
			const utensilIds = await resolveNames('utensils', current.utensils, base.utensils);
			const built = toVariantRecipeWrite(current, utensilIds);
			maps = built.maps;
			const text = description.trim();
			await onsave({
				name: name.trim(),
				...(text ? { description: text } : {}),
				visibility,
				recipe: built.recipe
			});
		} catch (e) {
			error = e;
			if (e instanceof ApiError) serverErrors = mapServerErrors(e.errors, maps, 'variant');
		} finally {
			saving = false;
		}
	}
</script>

<RecipeEditor
	bind:draft
	bind:guard
	mode="variant"
	{saving}
	{error}
	{serverErrors}
	{cancelHref}
	submitLabel={labels.save}
	onsubmit={submit}
>
	{#snippet banner()}
		<section class="flex flex-col gap-5" aria-labelledby="variant-about">
			<h2 id="variant-about" class="text-2xl">{labels.section}</h2>
			<TextField label={labels.name} bind:value={name} error={nameError} maxlength={80} />
			<TextArea label={labels.description} bind:value={description} optional />
			<VisibilitySelect label={labels.visibility} bind:value={visibility} {hint} />
		</section>
	{/snippet}
</RecipeEditor>
{@render children?.()}
