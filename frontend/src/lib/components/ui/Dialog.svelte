<script lang="ts">
	import X from '@lucide/svelte/icons/x';
	import { Dialog } from 'bits-ui';
	import type { Snippet } from 'svelte';
	import { t } from '$lib/i18n';
	import { iconButtonClass } from './variants';

	/**
	 * A modal task that needs the whole attention (e.g. deducting from the fridge). Full-screen on phones
	 * below 768 px, a centred card on larger screens. Focus is trapped; Escape and the close button
	 * close it. Put the buttons in `footer` (primary action last, so it sits on the right / bottom).
	 */
	type Props = {
		open?: boolean;
		title: string;
		description?: string;
		/** Optional trigger; receives props to spread on your own button. */
		trigger?: Snippet<[{ props: Record<string, unknown> }]>;
		/** `md` 32 rem, `lg` 44 rem (desktop width). */
		size?: 'md' | 'lg';
		onOpenChange?: (open: boolean) => void;
		children: Snippet;
		footer?: Snippet;
	};

	let {
		open = $bindable(false),
		title,
		description,
		trigger,
		size = 'md',
		onOpenChange,
		children,
		footer
	}: Props = $props();
</script>

<Dialog.Root bind:open {onOpenChange}>
	{#if trigger}
		<Dialog.Trigger>
			{#snippet child({ props })}
				{@render trigger({ props })}
			{/snippet}
		</Dialog.Trigger>
	{/if}
	<Dialog.Portal>
		<Dialog.Overlay class="fixed inset-0 z-40 bg-scrim" />
		<Dialog.Content
			class={[
				'fixed inset-0 z-50 flex flex-col bg-surface text-ink',
				'pt-(--rl-safe-top) pb-(--rl-safe-bottom)',
				'md:inset-auto md:top-1/2 md:left-1/2 md:max-h-[min(85dvh,48rem)] md:w-[calc(100%-3rem)] md:-translate-x-1/2 md:-translate-y-1/2 md:rounded-sheet md:p-0 md:shadow-overlay',
				size === 'lg' ? 'md:max-w-176' : 'md:max-w-128'
			]}
		>
			<header class="flex min-h-14 items-start gap-2 border-b border-line py-2 pr-2 pl-4">
				<div class="flex-1 py-2">
					<Dialog.Title class="font-serif text-xl font-semibold">{title}</Dialog.Title>
					{#if description}
						<Dialog.Description class="mt-1 text-sm text-muted">{description}</Dialog.Description>
					{/if}
				</div>
				<Dialog.Close class={iconButtonClass('ghost', 'md')} aria-label={t.common.actions.close}>
					<X size={22} aria-hidden="true" />
				</Dialog.Close>
			</header>
			<div class="flex-1 overflow-y-auto px-4 py-4">
				{@render children()}
			</div>
			{#if footer}
				<footer
					class="flex flex-col-reverse gap-2 border-t border-line px-4 py-3 md:flex-row md:justify-end"
				>
					{@render footer()}
				</footer>
			{/if}
		</Dialog.Content>
	</Dialog.Portal>
</Dialog.Root>
