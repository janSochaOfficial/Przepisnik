<script lang="ts">
	import type { Snippet } from 'svelte';
	import type { HTMLButtonAttributes } from 'svelte/elements';
	import Spinner from './Spinner.svelte';
	import type { ButtonSize, ButtonVariant, IconComponent } from './types';
	import { buttonClass } from './variants';

	type Props = Omit<HTMLButtonAttributes, 'class'> & {
		variant?: ButtonVariant;
		size?: ButtonSize;
		/** Shows a spinner, keeps the label for screen readers and blocks clicks. */
		loading?: boolean;
		/** Leading icon. */
		icon?: IconComponent;
		fullWidth?: boolean;
		/** Navigates instead of acting: renders an `<a>` with the same look. */
		href?: string;
		class?: string;
		children: Snippet;
	};

	let {
		variant = 'primary',
		size = 'md',
		loading = false,
		icon: Icon,
		fullWidth = false,
		href,
		type = 'button',
		disabled = false,
		class: className = '',
		children,
		onclick,
		...rest
	}: Props = $props();

	let classes = $derived([buttonClass(variant, size, fullWidth), className]);
	let iconSize = $derived(size === 'lg' ? 22 : 20);
</script>

{#snippet content()}
	{#if loading}
		<Spinner size={iconSize} />
	{:else if Icon}
		<Icon size={iconSize} aria-hidden="true" class="shrink-0" />
	{/if}
	<span>{@render children()}</span>
{/snippet}

{#if href && !disabled}
	<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- callers pass a resolved href -->
	<a {href} class={classes}>{@render content()}</a>
{:else}
	<button
		{...rest}
		{type}
		class={classes}
		disabled={disabled || undefined}
		aria-disabled={loading || undefined}
		aria-busy={loading || undefined}
		onclick={(event) => {
			if (loading) {
				event.preventDefault();
				return;
			}
			onclick?.(event);
		}}
	>
		{@render content()}
	</button>
{/if}
