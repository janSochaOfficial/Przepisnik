<script lang="ts">
	import CircleAlert from '@lucide/svelte/icons/circle-alert';
	import { ApiError } from '$lib/api/client';
	import { errorMessage, t } from '$lib/i18n';
	import Button from './Button.svelte';

	/**
	 * Shows a failed request in words: `t.errors[error.code]` for an `ApiError`, the generic server
	 * message for anything else. With `retry`, offers `t.common.retry`. Renders nothing when
	 * `error` is null, so it can sit permanently above a form.
	 */
	type Props = {
		error: unknown;
		retry?: () => void | Promise<unknown>;
		/** `inline` = one line above a form; `block` = in place of a page section that failed to load. */
		variant?: 'inline' | 'block';
		class?: string;
	};

	let { error, retry, variant = 'inline', class: className = '' }: Props = $props();

	let message = $derived(
		error == null ? null : errorMessage(error instanceof ApiError ? error.code : 'server.error')
	);
	let retrying = $state(false);

	async function handleRetry() {
		if (!retry) return;
		retrying = true;
		try {
			await retry();
		} finally {
			retrying = false;
		}
	}
</script>

{#if message}
	<div
		role="alert"
		class={[
			'flex gap-3 rounded-card border border-beet/40 bg-beet-wash text-ink',
			variant === 'inline' ? 'items-start p-3' : 'flex-col items-center p-6 text-center',
			className
		]}
	>
		<CircleAlert
			size={variant === 'inline' ? 22 : 32}
			aria-hidden="true"
			class="shrink-0 text-beet"
		/>
		<div class={['flex-1', variant === 'inline' && 'pt-px']}>
			{#if variant === 'block'}
				<p class="font-serif text-lg font-semibold">{t.common.errorTitle}</p>
			{/if}
			<p class="text-base">{message}</p>
		</div>
		{#if retry}
			<Button variant="secondary" loading={retrying} onclick={handleRetry}>
				{t.common.retry}
			</Button>
		{/if}
	</div>
{/if}
