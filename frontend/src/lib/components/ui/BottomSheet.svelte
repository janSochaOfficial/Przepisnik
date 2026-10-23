<script lang="ts">
	import X from '@lucide/svelte/icons/x';
	import { Dialog, Popover } from 'bits-ui';
	import type { Snippet } from 'svelte';
	import { MediaQuery } from 'svelte/reactivity';
	import { t } from '$lib/i18n';
	import { iconButtonClass } from './variants';

	/**
	 * A small picker that belongs to one control (ingredient options, scaling). One component, two
	 * shapes: a sheet sliding up from the bottom edge below 768 px (reachable with the thumb), a
	 * popover anchored to the trigger from 768 px. In both, focus is trapped while open, Escape and
	 * the close button close it, and focus returns to the trigger.
	 * Content should be short: a list of choices or one field. A long form belongs in `Dialog`.
	 */
	type Props = {
		open?: boolean;
		title: string;
		description?: string;
		/** The control that opens the sheet; spread `props` on your own button. */
		trigger: Snippet<[{ props: Record<string, unknown> }]>;
		onOpenChange?: (open: boolean) => void;
		children: Snippet;
		/** Popover placement on desktop. */
		side?: 'bottom' | 'top' | 'left' | 'right';
		align?: 'start' | 'center' | 'end';
	};

	let {
		open = $bindable(false),
		title,
		description,
		trigger,
		onOpenChange,
		children,
		side = 'bottom',
		align = 'start'
	}: Props = $props();

	const uid = $props.id();
	const desktop = new MediaQuery('min-width: 48rem', false);
	let titleId = $derived(`bs-${uid}-title`);
	let descId = $derived(`bs-${uid}-desc`);
</script>

{#snippet closeButton()}
	<span class="sr-only">{t.common.actions.close}</span>
	<X size={22} aria-hidden="true" />
{/snippet}

{#if desktop.current}
	<Popover.Root bind:open {onOpenChange}>
		<Popover.Trigger>
			{#snippet child({ props })}
				{@render trigger({ props })}
			{/snippet}
		</Popover.Trigger>
		<Popover.Portal>
			<Popover.Content
				{side}
				{align}
				sideOffset={8}
				collisionPadding={16}
				role="dialog"
				aria-labelledby={titleId}
				aria-describedby={description ? descId : undefined}
				data-shape="popover"
				class="z-50 max-h-(--bits-popover-content-available-height) w-[min(24rem,calc(100vw-2rem))] overflow-y-auto rounded-card border border-line bg-surface text-ink shadow-overlay"
			>
				<header class="flex items-start gap-2 py-1 pr-1 pl-4">
					<div class="flex-1 py-2.5">
						<h2 id={titleId} class="font-serif text-lg font-semibold">{title}</h2>
						{#if description}
							<p id={descId} class="mt-0.5 text-sm text-muted">{description}</p>
						{/if}
					</div>
					<Popover.Close class={iconButtonClass('ghost', 'md')}>
						{@render closeButton()}
					</Popover.Close>
				</header>
				<div class="px-4 pb-4">
					{@render children()}
				</div>
			</Popover.Content>
		</Popover.Portal>
	</Popover.Root>
{:else}
	<Dialog.Root bind:open {onOpenChange}>
		<Dialog.Trigger>
			{#snippet child({ props })}
				{@render trigger({ props })}
			{/snippet}
		</Dialog.Trigger>
		<Dialog.Portal>
			<Dialog.Overlay class="fixed inset-0 z-40 bg-scrim" />
			<Dialog.Content
				data-shape="sheet"
				class="fixed animate-rl-sheet-in inset-x-0 bottom-0 z-50 flex max-h-[85dvh] flex-col rounded-t-sheet bg-surface pb-(--rl-safe-bottom) text-ink shadow-overlay"
			>
				<div
					class="mx-auto mt-2 h-1 w-10 shrink-0 rounded-full bg-line-strong"
					aria-hidden="true"
				></div>
				<header class="flex items-start gap-2 pr-2 pl-4">
					<div class="flex-1 py-2.5">
						<Dialog.Title class="font-serif text-lg font-semibold">{title}</Dialog.Title>
						{#if description}
							<Dialog.Description class="mt-0.5 text-sm text-muted"
								>{description}</Dialog.Description
							>
						{/if}
					</div>
					<Dialog.Close class={iconButtonClass('ghost', 'md')}>
						{@render closeButton()}
					</Dialog.Close>
				</header>
				<div class="overflow-y-auto px-4 pb-4">
					{@render children()}
				</div>
			</Dialog.Content>
		</Dialog.Portal>
	</Dialog.Root>
{/if}
