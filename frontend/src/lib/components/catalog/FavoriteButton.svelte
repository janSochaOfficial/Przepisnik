<script lang="ts">
	import Heart from '@lucide/svelte/icons/heart';
	import { toast } from '$lib/components/ui';
	import { iconButtonClass } from '$lib/components/ui/variants';
	import { t } from '$lib/i18n';
	import { setFavorite, type FavoriteKind } from './favorites';

	/**
	 * The ♥ toggle of a recipe or a variant (PROJECT §4.6). Optimistic: the heart flips at once and
	 * flips back with a toast when the request fails. `onchange` runs after the server accepted.
	 */
	type Props = {
		kind: FavoriteKind;
		id: string;
		favorite?: boolean;
		class?: string;
		onchange?: (favorite: boolean) => void;
	};

	let { kind, id, favorite = $bindable(false), class: className = '', onchange }: Props = $props();

	let busy = $state(false);

	async function toggle() {
		if (busy) return;
		const next = !favorite;
		favorite = next;
		busy = true;
		try {
			await setFavorite(kind, id, next);
			onchange?.(next);
		} catch {
			favorite = !next;
			toast.error(t.recipes.catalog.favoriteFailed);
		} finally {
			busy = false;
		}
	}
</script>

<button
	type="button"
	aria-pressed={favorite}
	aria-label={favorite ? t.recipes.favorite.remove : t.recipes.favorite.add}
	title={favorite ? t.recipes.favorite.remove : t.recipes.favorite.add}
	class={[iconButtonClass('ghost', 'md'), favorite && 'text-cobalt', className]}
	onclick={toggle}
>
	<Heart size={22} aria-hidden="true" fill={favorite ? 'currentColor' : 'none'} />
</button>
