<script lang="ts">
	import type { HTMLButtonAttributes } from 'svelte/elements';
	import Spinner from './Spinner.svelte';
	import type { ButtonSize, ButtonVariant, IconComponent } from './types';
	import { iconButtonClass } from './variants';

	type Props = Omit<HTMLButtonAttributes, 'class' | 'children'> & {
		icon: IconComponent;
		/** Required: the accessible name (also shown as a tooltip). */
		label: string;
		variant?: ButtonVariant;
		size?: ButtonSize;
		loading?: boolean;
		/** Navigates instead of acting: renders an `<a>`. */
		href?: string;
		class?: string;
	};

	let {
		icon: Icon,
		label,
		variant = 'ghost',
		size = 'md',
		loading = false,
		href,
		type = 'button',
		disabled = false,
		class: className = '',
		onclick,
		...rest
	}: Props = $props();

	let classes = $derived([iconButtonClass(variant, size), className]);
	let iconSize = $derived(size === 'lg' ? 24 : 22);
</script>

{#if href && !disabled}
	<!-- eslint-disable-next-line svelte/no-navigation-without-resolve -- callers pass a resolved href -->
	<a {href} class={classes} aria-label={label} title={label}>
		<Icon size={iconSize} aria-hidden="true" />
	</a>
{:else}
	<button
		{...rest}
		{type}
		class={classes}
		aria-label={label}
		title={label}
		disabled={disabled || undefined}
		aria-disabled={loading || undefined}
		aria-busy={loading || undefined}
		onclick={(event) => {
			if (loading) return;
			onclick?.(event);
		}}
	>
		{#if loading}
			<Spinner size={iconSize} />
		{:else}
			<Icon size={iconSize} aria-hidden="true" />
		{/if}
	</button>
{/if}
