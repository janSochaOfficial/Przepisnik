<script lang="ts">
	import { Switch } from 'bits-ui';

	/** An on/off setting that applies at once. The whole row (label included) is the touch target. */
	type Props = {
		label: string;
		description?: string;
		checked?: boolean;
		disabled?: boolean;
		name?: string;
		class?: string;
		onCheckedChange?: (checked: boolean) => void;
	};

	let {
		label,
		description,
		checked = $bindable(false),
		disabled = false,
		name,
		class: className = '',
		onCheckedChange
	}: Props = $props();

	const uid = $props.id();
	let id = $derived(`sw-${uid}`);
</script>

<div class={['flex min-h-11 items-center justify-between gap-4', className]}>
	<div class="flex flex-col">
		<label for={id} class="text-base font-semibold text-ink">{label}</label>
		{#if description}
			<span id={`${id}-desc`} class="text-sm text-muted">{description}</span>
		{/if}
	</div>
	<Switch.Root
		{id}
		bind:checked
		{disabled}
		{name}
		{onCheckedChange}
		aria-describedby={description ? `${id}-desc` : undefined}
		class="relative inline-flex h-8 w-13 shrink-0 cursor-pointer items-center rounded-full border-2 border-line-strong bg-sunken transition-colors duration-150 before:absolute before:-inset-1.5 disabled:cursor-not-allowed disabled:opacity-55 data-[state=checked]:border-cobalt data-[state=checked]:bg-cobalt"
	>
		<Switch.Thumb
			class="pointer-events-none block size-6 translate-x-0.5 rounded-full bg-surface shadow-raised ring-1 ring-line-strong transition-transform duration-150 data-[state=checked]:translate-x-5.5 data-[state=checked]:ring-0"
		/>
	</Switch.Root>
</div>
