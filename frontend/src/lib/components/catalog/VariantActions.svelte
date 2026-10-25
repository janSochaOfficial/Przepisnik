<script lang="ts">
	import Ellipsis from '@lucide/svelte/icons/ellipsis';
	import { resolve } from '$app/paths';
	import { createApi, unwrap } from '$lib/api/client';
	import { BottomSheet, ConfirmDialog, toast } from '$lib/components/ui';
	import { buttonClass, iconButtonClass } from '$lib/components/ui/variants';
	import type { Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import ChangeVisibilityDialog from './ChangeVisibilityDialog.svelte';

	/**
	 * The ⋯ menu of a variant the viewer authored (PROJECT §6.5, D-41): edit, change visibility, delete.
	 * Render it only for the author. Without an available base recipe only deletion is possible.
	 * Deleting and changing visibility happen here; the host is told through `ondeleted` / `onvisibility`
	 * so it can update its list in place.
	 */
	type Props = {
		variantId: string;
		name: string;
		/** Unknown while a list is still loading it: the visibility item stays disabled until then. */
		visibility?: Visibility;
		baseAvailable?: boolean;
		onvisibility?: (visibility: Visibility) => void;
		ondeleted?: () => void;
		class?: string;
	};

	let {
		variantId,
		name,
		visibility,
		baseAvailable = true,
		onvisibility,
		ondeleted,
		class: className = ''
	}: Props = $props();

	const labels = t.variants.variantMenu;
	let menuOpen = $state(false);
	let visibilityOpen = $state(false);
	let deleteOpen = $state(false);

	async function remove() {
		try {
			await unwrap(createApi().DELETE('/variants/{id}', { params: { path: { id: variantId } } }));
		} catch (error) {
			toast.error(t.errors['server.error']);
			throw error;
		}
		toast.success(labels.deleted);
		ondeleted?.();
	}

	const item = buttonClass('ghost', 'md') + ' w-full justify-start';
</script>

<BottomSheet bind:open={menuOpen} title={labels.title}>
	{#snippet trigger({ props })}
		<button
			{...props}
			type="button"
			class={iconButtonClass('ghost', 'md') + ' relative z-10 shrink-0 ' + className}
			aria-label={labels.open(name)}
		>
			<Ellipsis size={22} aria-hidden="true" />
		</button>
	{/snippet}
	<ul class="flex flex-col">
		{#if baseAvailable}
			<li>
				<a class={item} href={resolve('/warianty/[id]/edytuj', { id: variantId })}>{labels.edit}</a>
			</li>
			<li>
				<button
					type="button"
					class={item}
					disabled={!visibility}
					onclick={() => {
						menuOpen = false;
						visibilityOpen = true;
					}}>{labels.changeVisibility}</button
				>
			</li>
		{/if}
		<li>
			<button
				type="button"
				class={item + ' text-beet'}
				onclick={() => {
					menuOpen = false;
					deleteOpen = true;
				}}>{labels.delete}</button
			>
		</li>
	</ul>
</BottomSheet>

{#if visibility}
	<ChangeVisibilityDialog
		bind:open={visibilityOpen}
		kind="variant"
		id={variantId}
		{visibility}
		onVariantChanged={(updated) => onvisibility?.(updated.visibility)}
	/>
{/if}

<ConfirmDialog
	bind:open={deleteOpen}
	title={labels.deleteTitle(name)}
	description={labels.deleteText}
	confirmLabel={labels.delete}
	onConfirm={remove}
/>
