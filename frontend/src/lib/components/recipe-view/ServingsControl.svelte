<script lang="ts">
	import Minus from '@lucide/svelte/icons/minus';
	import Plus from '@lucide/svelte/icons/plus';
	import { iconButtonClass } from '$lib/components/ui/variants';
	import {
		formatServings,
		formatServingsNumber,
		SERVINGS_MAX,
		SERVINGS_MIN
	} from '$lib/domain/scaling';
	import { t } from '$lib/i18n';
	import type { SelectionState } from '$lib/state/selection.svelte';

	/**
	 * "Porcje: [−] N [+]" (PROJECT §5.7). Choosing a number scales the recipe by chosen / servings,
	 * as the single scaling choice. Rendered only when the shown recipe states its servings.
	 */
	type Props = { state: SelectionState };

	let { state: selection }: Props = $props();

	const labels = t.variants.servings;
	const uid = $props.id();

	let shown = $derived(selection.shownServings);
	/** Scaled from an ingredient: the line shows `servings × factor` rounded to ½. */
	let fromIngredient = $derived(
		selection.scaling !== null && selection.scaling.kind !== 'servings' && selection.factor !== 1
	);
	let printed = $derived(
		shown ? `${shown.approximate ? '~' : ''}${formatServingsNumber(shown.value)}` : ''
	);

	// What the user is typing; null = show the current value.
	let typed = $state<string | null>(null);
	let invalid = $state(false);

	function choose(n: number) {
		typed = null;
		invalid = false;
		selection.setServings(n);
	}

	function commit() {
		if (typed === null) return;
		const text = typed.trim();
		const n = /^\d+$/.test(text) ? Number(text) : NaN;
		if (Number.isInteger(n) && n >= SERVINGS_MIN && n <= SERVINGS_MAX) {
			choose(n);
		} else {
			invalid = true;
		}
	}

	// − / + step from the shown value: 3 ½ goes to 3 or 4.
	let down = $derived(shown ? Math.ceil(shown.value) - 1 : SERVINGS_MIN);
	let up = $derived(shown ? Math.floor(shown.value) + 1 : SERVINGS_MIN);
</script>

{#if shown}
	<div class="mt-3 flex flex-col gap-1">
		<div class="flex flex-wrap items-center gap-2">
			<label for="servings-{uid}" class="text-base font-semibold">{labels.label}</label>
			<button
				type="button"
				class={iconButtonClass('secondary', 'md')}
				disabled={down < SERVINGS_MIN}
				onclick={() => choose(down)}
			>
				<span class="sr-only">{labels.decrease}</span>
				<Minus size={20} aria-hidden="true" />
			</button>
			<input
				id="servings-{uid}"
				type="text"
				inputmode="numeric"
				autocomplete="off"
				aria-invalid={invalid}
				aria-describedby={invalid ? `servings-error-${uid}` : undefined}
				class="min-h-12 w-20 rounded-control border border-line-strong bg-surface px-2 text-center text-base text-ink"
				value={typed ?? printed}
				oninput={(e) => {
					typed = e.currentTarget.value;
					invalid = false;
				}}
				onchange={commit}
				onkeydown={(e) => {
					if (e.key === 'Enter') commit();
					if (e.key === 'Escape') {
						typed = null;
						invalid = false;
					}
				}}
				onblur={() => {
					if (!invalid) typed = null;
				}}
			/>
			<button
				type="button"
				class={iconButtonClass('secondary', 'md')}
				disabled={up > SERVINGS_MAX}
				onclick={() => choose(up)}
			>
				<span class="sr-only">{labels.increase}</span>
				<Plus size={20} aria-hidden="true" />
			</button>
		</div>
		{#if invalid}
			<p id="servings-error-{uid}" class="text-sm text-beet">{labels.range}</p>
		{:else if fromIngredient}
			<p class="text-sm text-muted">
				{labels.fromIngredient(formatServings(shown.value, shown.approximate))}
			</p>
		{/if}
	</div>
{/if}
