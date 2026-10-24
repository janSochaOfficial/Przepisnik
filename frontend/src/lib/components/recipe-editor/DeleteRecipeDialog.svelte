<script lang="ts">
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import { ConfirmDialog, toast } from '$lib/components/ui';
	import { errorMessage, t } from '$lib/i18n';

	/**
	 * Confirms and runs `DELETE /recipes/{id}`. When other users have variants the server archives
	 * the recipe instead of deleting it (PROJECT §4.4), and the texts say so.
	 */
	type Props = {
		open?: boolean;
		recipeId: string;
		/** `foreignVariantCount` of the recipe detail. */
		foreignVariantCount: number;
		onDeleted?: (archived: boolean) => void;
	};

	let { open = $bindable(false), recipeId, foreignVariantCount, onDeleted }: Props = $props();

	let archives = $derived(foreignVariantCount > 0);

	async function remove() {
		try {
			await unwrap(createApi().DELETE('/recipes/{id}', { params: { path: { id: recipeId } } }));
		} catch (error) {
			toast.error(errorMessage(error instanceof ApiError ? error.code : 'server.error'));
			throw error;
		}
		toast.success(archives ? t.recipes.delete.archived : t.recipes.delete.deleted);
		onDeleted?.(archives);
	}
</script>

<ConfirmDialog
	bind:open
	title={t.recipes.delete.title}
	description={archives ? t.recipes.delete.textArchive : t.recipes.delete.text}
	confirmLabel={t.recipes.delete.confirm}
	onConfirm={remove}
/>
