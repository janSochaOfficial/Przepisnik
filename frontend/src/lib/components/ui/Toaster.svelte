<script lang="ts">
	import CircleAlert from '@lucide/svelte/icons/circle-alert';
	import CircleCheck from '@lucide/svelte/icons/circle-check';
	import X from '@lucide/svelte/icons/x';
	import { t } from '$lib/i18n';
	import { toast as defaultQueue, type ToastItem, type ToastQueue } from './toast.svelte';

	/**
	 * Renders the toast queue. Mounted once by the root layout; tests may pass their own `queue`.
	 * Sits above the bottom tab bar on phones and in the bottom-right corner on desktop.
	 */
	let { queue = defaultQueue }: { queue?: ToastQueue } = $props();
</script>

<section
	aria-label={t.common.ui.notifications}
	onpointerenter={() => queue.pauseAll()}
	onpointerleave={() => queue.resumeAll()}
	onfocusin={() => queue.pauseAll()}
	onfocusout={() => queue.resumeAll()}
	class="pointer-events-none fixed inset-x-0 bottom-[calc(var(--rl-tabbar-h)+var(--rl-safe-bottom)+var(--rl-actionbar-h,0px)+0.75rem)] z-60 flex flex-col items-center gap-2 px-4 lg:right-6 lg:bottom-[calc(var(--rl-actionbar-h,0px)+1.5rem)] lg:left-auto lg:items-end lg:px-0"
>
	<!-- Live regions must exist before content is added, so both are always rendered. -->
	<div
		role="status"
		aria-live="polite"
		class="flex w-full flex-col items-center gap-2 lg:items-end"
	>
		{#each queue.items.filter((i) => i.tone !== 'error') as item (item.id)}
			{@render toastItem(item)}
		{/each}
	</div>
	<div
		role="alert"
		aria-live="assertive"
		class="flex w-full flex-col items-center gap-2 lg:items-end"
	>
		{#each queue.items.filter((i) => i.tone === 'error') as item (item.id)}
			{@render toastItem(item)}
		{/each}
	</div>
</section>

{#snippet toastItem(item: ToastItem)}
	<div
		class={[
			'pointer-events-auto flex w-full max-w-md animate-rl-toast-in items-center gap-3 rounded-card py-1.5 pr-1.5 pl-4 shadow-overlay',
			'bg-ink text-paper'
		]}
	>
		{#if item.tone === 'success'}
			<CircleCheck size={20} aria-hidden="true" class="shrink-0" />
		{:else if item.tone === 'error'}
			<CircleAlert size={20} aria-hidden="true" class="shrink-0" />
		{/if}
		<p class="flex-1 py-2 text-base">{item.message}</p>
		{#if item.action}
			<button
				type="button"
				class="min-h-11 shrink-0 rounded-control px-3 font-semibold text-paper underline decoration-2 underline-offset-4 hover:bg-white/10"
				onclick={() => queue.runAction(item.id)}
			>
				{item.action.label}
			</button>
		{/if}
		<button
			type="button"
			class="inline-flex size-11 shrink-0 items-center justify-center rounded-control text-paper/80 hover:bg-white/10 hover:text-paper"
			aria-label={t.common.ui.dismiss}
			onclick={() => queue.dismiss(item.id)}
		>
			<X size={20} aria-hidden="true" />
		</button>
	</div>
{/snippet}
