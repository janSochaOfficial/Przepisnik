<script lang="ts">
	import ArrowDown from '@lucide/svelte/icons/arrow-down';
	import ArrowUp from '@lucide/svelte/icons/arrow-up';
	import Plus from '@lucide/svelte/icons/plus';
	import Trash from '@lucide/svelte/icons/trash';
	import Undo2 from '@lucide/svelte/icons/undo-2';
	import { Button, IconButton, TextArea, TextField } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { canMoveImage, insertImageAt, moveImage, removeBlock, type DraftBlock } from './blocks';
	import { emptyStep, imageUrl, type DraftStep, type EditorMode } from './draft';
	import ImageUploadButton from './ImageUploadButton.svelte';
	import { canMove, moveRow } from './lists';

	/**
	 * The block editor for one section (Przygotowanie or Wykonanie): numbered steps, each a list of
	 * text and image blocks. "insert photo" splits the text at the cursor and puts the uploaded
	 * photo in between. In `variant` mode base steps keep their order and are removed with
	 * "remove step in variant" (the row stays, marked as removed).
	 */
	type Props = {
		steps: DraftStep[];
		/** Prefix of the field-error paths: `prepSteps` or `instructionSteps`. */
		path: 'prepSteps' | 'instructionSteps';
		mode?: EditorMode;
		errors?: Record<string, string>;
		/** Message shown when the list is empty. */
		emptyText?: string;
	};

	let { steps = $bindable(), path, mode = 'recipe', errors = {}, emptyText }: Props = $props();

	// Cursor offset of each text block, remembered when the textarea loses focus.
	const cursors: Record<string, number> = {};

	function track(block: DraftBlock, event: Event & { currentTarget: HTMLTextAreaElement }) {
		cursors[block.key] = event.currentTarget.selectionStart;
	}

	function insertPhoto(step: DraftStep, blockKey: string, cursor: number, imageId: string) {
		const index = step.blocks.findIndex((b) => b.key === blockKey);
		if (index >= 0) step.blocks = insertImageAt(step.blocks, index, cursor, imageId);
	}

	function removeStep(index: number) {
		const step = steps[index];
		if (!step) return;
		if (mode === 'variant' && step.base) step.removed = !step.removed;
		else steps = steps.filter((_, i) => i !== index);
	}

	let visibleCount = $derived(steps.filter((s) => !s.removed).length);
	let numbers = $derived(
		steps.map((s, i) => (s.removed ? 0 : steps.slice(0, i + 1).filter((x) => !x.removed).length))
	);
</script>

{#if steps.length === 0 && emptyText}
	<p class="text-base text-muted">{emptyText}</p>
{/if}

<ol class="flex flex-col gap-5">
	{#each steps as step, index (step.key)}
		<li
			aria-label={t.recipes.steps.step(numbers[index] || index + 1)}
			class={[
				'flex flex-col gap-3 rounded-card border border-line bg-surface p-3',
				step.removed && 'opacity-60'
			]}
		>
			<div class="flex items-center gap-1">
				<h3 class="flex-1 font-sans text-base font-semibold">
					{t.recipes.steps.step(numbers[index] || index + 1)}
				</h3>
				{#if !step.removed}
					<IconButton
						icon={ArrowUp}
						label={t.recipes.steps.moveStepUp}
						disabled={!canMove(steps, index, 'up') || (mode === 'variant' && step.base)}
						onclick={() => (steps = moveRow(steps, index, 'up'))}
					/>
					<IconButton
						icon={ArrowDown}
						label={t.recipes.steps.moveStepDown}
						disabled={!canMove(steps, index, 'down') || (mode === 'variant' && step.base)}
						onclick={() => (steps = moveRow(steps, index, 'down'))}
					/>
				{/if}
				{#if step.removed}
					<Button variant="secondary" icon={Undo2} onclick={() => removeStep(index)}>
						{t.recipes.steps.restoreStep}
					</Button>
				{:else}
					<IconButton
						icon={Trash}
						label={mode === 'variant' && step.base
							? t.recipes.steps.removeStepInVariant
							: t.recipes.steps.removeStep}
						onclick={() => removeStep(index)}
					/>
				{/if}
			</div>

			{#if step.removed}
				<p class="text-sm text-muted">{t.recipes.steps.removedInVariant}</p>
			{:else}
				{#each step.blocks as block, blockIndex (block.key)}
					{#if block.type === 'text'}
						<div class="flex flex-col gap-2">
							<TextArea
								label={t.recipes.steps.text}
								hideLabel
								rows={blockIndex === 0 && step.blocks.length === 1 ? 4 : 2}
								placeholder={t.recipes.steps.textPlaceholder}
								bind:value={block.text}
								onblur={(e) => track(block, e)}
								onkeyup={(e) => track(block, e)}
								onclick={(e) => track(block, e)}
							/>
							<div>
								<ImageUploadButton
									label={t.recipes.steps.insertImage}
									onUploaded={(image) =>
										insertPhoto(step, block.key, cursors[block.key] ?? block.text.length, image.id)}
								/>
							</div>
						</div>
					{:else}
						<figure class="flex flex-col gap-2 rounded-control border border-line p-2">
							<img
								src={imageUrl(block.imageId, 'thumb')}
								alt={block.caption || t.recipes.steps.imageAlt}
								class="max-h-64 w-full rounded-control object-contain"
							/>
							<TextField
								label={t.recipes.steps.caption}
								optional
								maxlength={300}
								bind:value={block.caption}
							/>
							<div class="flex gap-1">
								<IconButton
									icon={ArrowUp}
									label={t.recipes.steps.moveImageUp}
									disabled={!canMoveImage(step.blocks, blockIndex, 'up')}
									onclick={() => (step.blocks = moveImage(step.blocks, blockIndex, 'up'))}
								/>
								<IconButton
									icon={ArrowDown}
									label={t.recipes.steps.moveImageDown}
									disabled={!canMoveImage(step.blocks, blockIndex, 'down')}
									onclick={() => (step.blocks = moveImage(step.blocks, blockIndex, 'down'))}
								/>
								<IconButton
									icon={Trash}
									label={t.recipes.steps.removeImage}
									onclick={() => (step.blocks = removeBlock(step.blocks, blockIndex))}
								/>
							</div>
						</figure>
					{/if}
				{/each}
				{@const message = errors[`${path}[${index}]`]}
				{#if message}
					<p class="text-sm font-semibold text-beet" role="alert">{message}</p>
				{/if}
			{/if}
		</li>
	{/each}
</ol>

{#if errors[path]}
	<p class="mt-2 text-sm font-semibold text-beet" role="alert">{errors[path]}</p>
{/if}

<div class="mt-4">
	<Button variant="secondary" icon={Plus} onclick={() => (steps = [...steps, emptyStep()])}>
		{t.recipes.steps.addStep}
	</Button>
</div>
{#if visibleCount === 0 && steps.length > 0}
	<p class="mt-2 text-sm text-muted">{emptyText}</p>
{/if}
