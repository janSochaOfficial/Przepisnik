<script lang="ts">
	import { untrack } from 'svelte';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import type { components } from '$lib/api/schema';
	import { Button, Dialog, ErrorMessage, toast } from '$lib/components/ui';
	import type { Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import VisibilitySelect from './VisibilitySelect.svelte';

	/**
	 * The change-visibility dialog (PROJECT §4.4): `PUT /recipes/{id}/visibility`, or with
	 * `kind="variant"` `PUT /variants/{id}/visibility` (never locked, §6.1). Opened from the recipe
	 * menu (F5) and the variant menu (V2). Narrowing a recipe is refused while other users have
	 * variants (409 `recipe.visibility_locked`), which the dialog explains instead of showing a
	 * generic error.
	 */
	type Props = {
		open?: boolean;
		kind?: 'recipe' | 'variant';
		/** The recipe's or the variant's id, by `kind`. */
		id: string;
		visibility: Visibility;
		/** Called with the updated recipe after a successful change (`kind="recipe"`). */
		onChanged?: (recipe: components['schemas']['RecipeDetail']) => void;
		/** Called with the updated variant after a successful change (`kind="variant"`). */
		onVariantChanged?: (variant: components['schemas']['VariantDetail']) => void;
	};

	let {
		open = $bindable(false),
		kind = 'recipe',
		id,
		visibility,
		onChanged,
		onVariantChanged
	}: Props = $props();

	// Writable deriveds: they start over (current visibility, no error) whenever the dialog opens or closes.
	let selected = $derived.by((): Visibility => {
		void open;
		return untrack(() => visibility);
	});
	let error = $derived.by((): unknown => {
		void open;
		return null;
	});
	let saving = $state(false);

	let locked = $derived(error instanceof ApiError && error.code === 'recipe.visibility_locked');
	let foreignCount = $derived(
		error instanceof ApiError && typeof error.problem?.foreignVariantCount === 'number'
			? error.problem.foreignVariantCount
			: null
	);

	async function save() {
		saving = true;
		error = null;
		try {
			if (kind === 'variant') {
				const variant = await unwrap(
					createApi().PUT('/variants/{id}/visibility', {
						params: { path: { id } },
						body: { visibility: selected }
					})
				);
				toast.success(t.recipes.changeVisibility.saved);
				open = false;
				onVariantChanged?.(variant);
			} else {
				const recipe = await unwrap(
					createApi().PUT('/recipes/{id}/visibility', {
						params: { path: { id } },
						body: { visibility: selected }
					})
				);
				toast.success(t.recipes.changeVisibility.saved);
				open = false;
				onChanged?.(recipe);
			}
		} catch (e) {
			error = e;
		} finally {
			saving = false;
		}
	}
</script>

<Dialog bind:open title={t.recipes.changeVisibility.title}>
	<div class="flex flex-col gap-4">
		{#if locked}
			<div role="alert" class="rounded-card border border-beet/40 bg-beet-wash p-3 text-base">
				<p>{t.recipes.changeVisibility.locked}</p>
				{#if foreignCount !== null}
					<p class="mt-1 text-sm text-muted">
						{t.recipes.changeVisibility.lockedCount(foreignCount)}
					</p>
				{/if}
			</div>
		{:else}
			<ErrorMessage {error} />
		{/if}
		<VisibilitySelect bind:value={selected} />
	</div>
	{#snippet footer()}
		<Button variant="secondary" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button loading={saving} disabled={selected === visibility} onclick={save}>
			{t.recipes.changeVisibility.save}
		</Button>
	{/snippet}
</Dialog>
