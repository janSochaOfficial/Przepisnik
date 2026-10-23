<script lang="ts">
	import { Slider } from 'bits-ui';
	import { t } from '$lib/i18n';

	/**
	 * One value on a range; by default the "how much is left" percentage (0–100, step 5, PROJECT §8.2).
	 * The current value is printed next to the label with `format` (default: "45 %").
	 */
	type Props = {
		label: string;
		value?: number;
		min?: number;
		max?: number;
		step?: number;
		format?: (value: number) => string;
		disabled?: boolean;
		class?: string;
		/** Every change while dragging. */
		onValueChange?: (value: number) => void;
		/** Once, when the user lets go (use this one to save). */
		onValueCommit?: (value: number) => void;
	};

	const percent = (v: number) => t.common.ui.percent(new Intl.NumberFormat('pl-PL').format(v));

	let {
		label,
		value = $bindable(100),
		min = 0,
		max = 100,
		step = 5,
		format = percent,
		disabled = false,
		class: className = '',
		onValueChange,
		onValueCommit
	}: Props = $props();

	const uid = $props.id();
	let labelId = $derived(`sl-${uid}-label`);
	let text = $derived(format(value));
</script>

<div class={['flex flex-col gap-2', className]}>
	<div class="flex items-baseline justify-between gap-4">
		<span id={labelId} class="text-sm font-semibold text-ink">{label}</span>
		<output class="tabular text-base font-semibold text-ink" aria-hidden="true">{text}</output>
	</div>
	<Slider.Root
		type="single"
		bind:value
		{min}
		{max}
		{step}
		{disabled}
		onValueChange={(v) => onValueChange?.(v)}
		onValueCommit={(v) => onValueCommit?.(v)}
		class="relative flex h-11 w-full touch-none items-center select-none data-disabled:opacity-55"
	>
		<span class="relative h-2 w-full grow overflow-hidden rounded-full bg-sunken">
			<Slider.Range class="absolute h-full bg-cobalt" />
		</span>
		<Slider.Thumb
			index={0}
			aria-labelledby={labelId}
			aria-valuetext={text}
			class="block size-7 cursor-grab rounded-full border-2 border-cobalt bg-surface shadow-raised active:cursor-grabbing"
		/>
	</Slider.Root>
</div>
