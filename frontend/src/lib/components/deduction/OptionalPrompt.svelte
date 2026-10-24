<script lang="ts">
	import { AlertDialog } from 'bits-ui';
	import { buttonClass } from '$lib/components/ui/variants';
	import { t } from '$lib/i18n';

	/**
	 * Asks whether missing optional ingredients go on the shopping list too (PROJECT §9, D-33).
	 * "Tylko wymagane" is focused first; Escape or a click outside cancels (`onAnswer(null)`).
	 */
	type Props = {
		names: string;
		onAnswer: (includeOptional: boolean | null) => void;
	};

	let { names, onAnswer }: Props = $props();
	let open = $state(true);
	let answered = false;
	const labels = t.shopping.optionalPrompt;

	function answer(value: boolean | null) {
		if (answered) return;
		answered = true;
		open = false;
		onAnswer(value);
	}
</script>

<AlertDialog.Root bind:open onOpenChange={(next) => !next && answer(null)}>
	<AlertDialog.Portal>
		<AlertDialog.Overlay class="fixed inset-0 z-[60] bg-scrim" />
		<AlertDialog.Content
			class="fixed inset-x-4 bottom-[calc(1rem+var(--rl-safe-bottom))] z-[70] mx-auto max-w-md rounded-sheet bg-surface p-5 text-ink shadow-overlay md:top-1/2 md:bottom-auto md:-translate-y-1/2"
		>
			<AlertDialog.Title class="font-serif text-xl font-semibold">{labels.title}</AlertDialog.Title>
			<AlertDialog.Description class="mt-2 text-base text-muted">
				{labels.description(names)}
			</AlertDialog.Description>
			<div class="mt-6 flex flex-col-reverse gap-2 sm:flex-row sm:justify-end">
				<button type="button" class={buttonClass('secondary', 'md')} onclick={() => answer(true)}>
					{labels.includeOptional}
				</button>
				<AlertDialog.Cancel class={buttonClass('primary', 'md')} onclick={() => answer(false)}>
					{labels.requiredOnly}
				</AlertDialog.Cancel>
			</div>
		</AlertDialog.Content>
	</AlertDialog.Portal>
</AlertDialog.Root>
