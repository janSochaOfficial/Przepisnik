<script lang="ts">
	import Minus from '@lucide/svelte/icons/minus';
	import Plus from '@lucide/svelte/icons/plus';
	import { untrack } from 'svelte';
	import { ApiError, createApi, unwrap } from '$lib/api/client';
	import {
		Button,
		Dialog,
		ErrorMessage,
		IconButton,
		NumberField,
		Slider,
		TextField,
		toast
	} from '$lib/components/ui';
	import type { FridgeItem, LiquidUnits } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import { baseUnitLabel, inventoryText } from './amount';

	/**
	 * Edits one package (PROJECT §8.2): remaining percent with a slider in 5 % steps, or whole pieces for
	 * `count` products; the package amount; a note; and delete. A percent of 0 removes the item.
	 * Mount it only while open (`{#if}`), so each use starts from the item's current state.
	 */
	type Props = {
		open?: boolean;
		fridgeId: string;
		item: FridgeItem;
		liquidUnits: LiquidUnits;
		onSaved: (item: FridgeItem) => void;
		/** The item is gone (deleted, or emptied to 0 %). */
		onRemoved: (item: FridgeItem) => void;
		/** Someone else changed it first: reload the fridge. */
		onConflict: () => void;
	};

	let {
		open = $bindable(false),
		fridgeId,
		item,
		liquidUnits,
		onSaved,
		onRemoved,
		onConflict
	}: Props = $props();

	const labels = t.fridge.edit;
	const isCount = untrack(() => item.product.measureKind === 'count');

	let packageAmount = $state<number | null>(untrack(() => item.packageAmount));
	let percent = $state(untrack(() => Math.round(item.remainingPercent / 5) * 5));
	let piecesLeft = $state(untrack(() => Math.round(item.remainingAmount)));
	let note = $state(untrack(() => item.note ?? ''));
	let percentTouched = $state(false);
	let saving = $state(false);
	let deleting = $state(false);
	let error = $state<unknown>(null);
	let submitted = $state(false);

	let packageError = $derived(
		submitted && !(packageAmount !== null && packageAmount > 0) ? labels.packageRequired : null
	);
	let unit = $derived(baseUnitLabel(item.product));

	function setPieces(next: number) {
		const max = Math.max(0, Math.round(packageAmount ?? item.packageAmount));
		piecesLeft = Math.min(max, Math.max(0, next));
		percentTouched = true;
	}

	/** The percent to send: the item's own value unless the user changed what is left. */
	function remainingToSend(pkg: number): number {
		if (!percentTouched) return item.remainingPercent;
		if (isCount) return Math.round((piecesLeft / pkg) * 10000) / 100;
		return percent;
	}

	async function save(event: SubmitEvent) {
		event.preventDefault();
		submitted = true;
		if (!(packageAmount !== null && packageAmount > 0) || saving) return;
		saving = true;
		error = null;
		try {
			const result = await unwrap(
				createApi().PUT('/fridges/{id}/items/{itemId}', {
					params: { path: { id: fridgeId, itemId: item.id } },
					body: {
						productId: item.product.id,
						packageAmount,
						remainingPercent: remainingToSend(packageAmount),
						version: item.version,
						...(note.trim() ? { note: note.trim() } : {})
					}
				})
			);
			open = false;
			if (result) {
				toast.success(labels.saved);
				onSaved(result);
			} else {
				toast.success(labels.deleted);
				onRemoved(item);
			}
		} catch (e) {
			handleError(e);
		} finally {
			saving = false;
		}
	}

	async function remove() {
		deleting = true;
		error = null;
		try {
			await unwrap(
				createApi().DELETE('/fridges/{id}/items/{itemId}', {
					params: { path: { id: fridgeId, itemId: item.id } }
				})
			);
			open = false;
			onRemoved(item);
		} catch (e) {
			handleError(e);
		} finally {
			deleting = false;
		}
	}

	function handleError(e: unknown) {
		if (e instanceof ApiError && e.status === 409) {
			open = false;
			toast.error(labels.conflict);
			onConflict();
			return;
		}
		error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
	}
</script>

<Dialog bind:open title={labels.title(item.product.name)}>
	<form id="edit-item-form" class="flex flex-col gap-5" onsubmit={save}>
		<ErrorMessage {error} />
		{#if isCount}
			<div class="flex flex-col gap-2">
				<span id="pieces-label" class="text-sm font-semibold text-ink">{labels.piecesLabel}</span>
				<div class="flex items-center gap-3" role="group" aria-labelledby="pieces-label">
					<IconButton
						icon={Minus}
						label={labels.decrease}
						variant="secondary"
						disabled={piecesLeft <= 0}
						onclick={() => setPieces(piecesLeft - 1)}
					/>
					<output class="tabular min-w-20 text-center text-lg font-semibold text-ink">
						{piecesLeft} / {Math.round(packageAmount ?? item.packageAmount)}
					</output>
					<IconButton
						icon={Plus}
						label={labels.increase}
						variant="secondary"
						disabled={piecesLeft >= Math.round(packageAmount ?? item.packageAmount)}
						onclick={() => setPieces(piecesLeft + 1)}
					/>
				</div>
			</div>
		{:else}
			<Slider
				label={labels.percentLabel}
				bind:value={percent}
				onValueChange={() => (percentTouched = true)}
			/>
			<p class="tabular -mt-3 text-sm text-muted">
				{inventoryText(
					((packageAmount ?? item.packageAmount) * percent) / 100,
					item.product,
					liquidUnits
				)}
			</p>
		{/if}
		<NumberField
			label={labels.packageLabel}
			bind:value={packageAmount}
			min={0}
			suffix={unit}
			error={packageError}
		/>
		<TextField label={labels.noteLabel} bind:value={note} optional maxlength={200} />
	</form>
	{#snippet footer()}
		<Button variant="danger" loading={deleting} onclick={remove}>{labels.delete}</Button>
		<Button type="submit" form="edit-item-form" loading={saving}>{labels.save}</Button>
	{/snippet}
</Dialog>
