<script lang="ts">
	import { AlertDialog } from 'bits-ui';
	import type { Snippet } from 'svelte';
	import { t } from '$lib/i18n';
	import Spinner from './Spinner.svelte';
	import { buttonClass } from './variants';

	/**
	 * Asks before a destructive or irreversible action. The confirm label names the action
	 * ("delete recipe", never "OK"). `onConfirm` may return a promise: the button shows a spinner and the
	 * dialog closes when it resolves; when it rejects the dialog stays open (show the error yourself).
	 * Escape and "Anuluj" cancel; focus starts on "Anuluj".
	 */
	type Props = {
		open?: boolean;
		title: string;
		description?: string;
		confirmLabel: string;
		cancelLabel?: string;
		tone?: 'danger' | 'primary';
		onConfirm: () => void | Promise<unknown>;
		onCancel?: () => void;
		trigger?: Snippet<[{ props: Record<string, unknown> }]>;
	};

	let {
		open = $bindable(false),
		title,
		description,
		confirmLabel,
		cancelLabel = t.common.actions.cancel,
		tone = 'danger',
		onConfirm,
		onCancel,
		trigger
	}: Props = $props();

	let busy = $state(false);

	async function confirm() {
		if (busy) return;
		busy = true;
		try {
			await onConfirm();
			open = false;
		} catch {
			// The caller reports the error; keep the dialog open so the user can retry or cancel.
		} finally {
			busy = false;
		}
	}
</script>

<AlertDialog.Root
	bind:open
	onOpenChange={(next) => {
		if (!next && !busy) onCancel?.();
	}}
>
	{#if trigger}
		<AlertDialog.Trigger>
			{#snippet child({ props })}
				{@render trigger({ props })}
			{/snippet}
		</AlertDialog.Trigger>
	{/if}
	<AlertDialog.Portal>
		<AlertDialog.Overlay class="fixed inset-0 z-40 bg-scrim" />
		<AlertDialog.Content
			class="fixed inset-x-4 bottom-[calc(1rem+var(--rl-safe-bottom))] z-50 mx-auto max-w-md rounded-sheet bg-surface p-5 text-ink shadow-overlay md:top-1/2 md:bottom-auto md:-translate-y-1/2"
		>
			<AlertDialog.Title class="font-serif text-xl font-semibold">{title}</AlertDialog.Title>
			{#if description}
				<AlertDialog.Description class="mt-2 text-base text-muted">
					{description}
				</AlertDialog.Description>
			{/if}
			<div class="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
				<AlertDialog.Cancel class={buttonClass('secondary', 'md')} disabled={busy}>
					{cancelLabel}
				</AlertDialog.Cancel>
				<button
					type="button"
					class={buttonClass(tone, 'md')}
					aria-busy={busy || undefined}
					onclick={confirm}
				>
					{#if busy}<Spinner />{/if}
					{confirmLabel}
				</button>
			</div>
		</AlertDialog.Content>
	</AlertDialog.Portal>
</AlertDialog.Root>
