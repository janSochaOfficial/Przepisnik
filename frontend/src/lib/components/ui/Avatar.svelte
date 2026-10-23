<script lang="ts">
	import { Avatar } from 'bits-ui';
	import { t } from '$lib/i18n';

	/**
	 * A person's picture, or their initials on a tint when there is none (or it fails to load).
	 * `decorative` hides it from screen readers when the name is printed next to it.
	 */
	type Props = {
		name: string;
		src?: string | null;
		size?: 'sm' | 'md' | 'lg';
		decorative?: boolean;
		class?: string;
	};

	let {
		name,
		src = null,
		size = 'md',
		decorative = false,
		class: className = ''
	}: Props = $props();

	let initials = $derived(
		name
			.split(/[\s_-]+/)
			.filter(Boolean)
			.slice(0, 2)
			.map((part) => part.charAt(0).toLocaleUpperCase('pl'))
			.join('')
	);
	let sizeClass = $derived(
		{ sm: 'size-8 text-xs', md: 'size-10 text-sm', lg: 'size-16 text-xl' }[size]
	);
</script>

<Avatar.Root
	class={[
		'inline-flex shrink-0 items-center justify-center overflow-hidden rounded-full bg-cobalt-wash font-semibold text-cobalt',
		sizeClass,
		className
	]}
	role={decorative ? undefined : 'img'}
	aria-label={decorative ? undefined : t.common.ui.avatarOf(name)}
	aria-hidden={decorative || undefined}
>
	{#if src}
		<Avatar.Image {src} alt="" class="size-full object-cover" />
	{/if}
	<Avatar.Fallback>{initials}</Avatar.Fallback>
</Avatar.Root>
