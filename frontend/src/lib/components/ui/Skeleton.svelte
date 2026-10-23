<script lang="ts">
	/**
	 * A grey placeholder in the shape of content that is loading. Decorative: the container that is
	 * loading sets `aria-busy="true"` and, for long waits, a visually hidden `t.common.loading`.
	 * Size it with classes (`class="h-5 w-40"`); `shape` picks the corner radius.
	 */
	type Props = {
		shape?: 'line' | 'block' | 'circle';
		class?: string;
	};

	let { shape = 'line', class: className = '' }: Props = $props();
</script>

<span
	aria-hidden="true"
	class={[
		'block animate-rl-pulse bg-sunken',
		shape === 'line' && 'h-4 rounded-full',
		shape === 'block' && 'h-24 rounded-card',
		shape === 'circle' && 'size-10 rounded-full',
		className
	]}
></span>
