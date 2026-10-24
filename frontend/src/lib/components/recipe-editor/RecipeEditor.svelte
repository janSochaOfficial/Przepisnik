<script lang="ts">
	import { beforeNavigate, goto } from '$app/navigation';
	import { onMount, type Snippet } from 'svelte';
	import VisibilitySelect from '$lib/components/catalog/VisibilitySelect.svelte';
	import { loadNames, type Named } from '$lib/components/catalog/names';
	import { ActionBar } from '$lib/components/shell';
	import {
		Button,
		ConfirmDialog,
		ErrorMessage,
		NumberField,
		TagInput,
		TextArea,
		TextField
	} from '$lib/components/ui';
	import type { Visibility } from '$lib/domain/types';
	import { t } from '$lib/i18n';
	import {
		draftSignature,
		imageUrl,
		validateDraft,
		type EditorMode,
		type FieldErrors,
		type RecipeDraft
	} from './draft';
	import ImageUploadButton from './ImageUploadButton.svelte';
	import IngredientList from './IngredientList.svelte';
	import StepList from './StepList.svelte';

	/**
	 * The recipe form, used by `/przepisy/nowy`, `/przepisy/[id]/edytuj` and (`mode="variant"`) by the
	 * variant editor. It owns client validation and the unsaved-changes guard; the page owns the
	 * request: `onsubmit` receives the validated draft, the page reports `error` (generic) and
	 * `serverErrors` (field paths already mapped with `mapServerErrors`).
	 *
	 * In `variant` mode title, description, cover and tags are hidden and base rows cannot be reordered.
	 */
	type Props = {
		draft: RecipeDraft;
		mode?: EditorMode;
		onsubmit: (draft: RecipeDraft) => void | Promise<void>;
		saving?: boolean;
		error?: unknown;
		serverErrors?: FieldErrors;
		/** Where "Anuluj" goes. */
		cancelHref: string;
		submitLabel?: string;
		/** Shows the visibility choice (new recipes only). */
		visibility?: Visibility | null;
		/** Set to false right before navigating away after a successful save. */
		guard?: boolean;
		/** Rendered above the form, e.g. the "variants will be deleted" banner. */
		banner?: Snippet;
	};

	let {
		draft = $bindable(),
		mode = 'recipe',
		onsubmit,
		saving = false,
		error = null,
		serverErrors = {},
		cancelHref,
		submitLabel = t.recipes.editor.save,
		visibility = $bindable(null),
		guard = $bindable(true),
		banner
	}: Props = $props();

	const labels = t.recipes.editor;

	// What the form looked like when it opened; any difference is an unsaved change.
	const initial = draftSignature($state.snapshot(draft));
	let dirty = $derived(draftSignature(draft) !== initial);

	let submitted = $state(false);
	let clientErrors = $derived(submitted ? validateDraft(draft, mode) : {});
	let errors = $derived({ ...serverErrors, ...clientErrors });

	let tagSuggestions = $state<Named[]>([]);
	let utensilSuggestions = $state<Named[]>([]);
	onMount(async () => {
		[tagSuggestions, utensilSuggestions] = await Promise.all([
			loadNames('tags'),
			loadNames('utensils')
		]);
	});

	async function submit(event: SubmitEvent) {
		event.preventDefault();
		const form = event.currentTarget as HTMLFormElement;
		submitted = true;
		const found = validateDraft(draft, mode);
		if (Object.keys(found).length > 0) {
			queueMicrotask(() => {
				form.querySelector<HTMLElement>('[aria-invalid="true"], [role="alert"]')?.focus?.();
				form.querySelector('[role="alert"]')?.scrollIntoView?.({ block: 'center' });
			});
			return;
		}
		await onsubmit(draft);
	}

	// Unsaved-changes guard: in-app navigation asks first; closing the tab uses the browser prompt.
	let leaveUrl = $state<URL | null>(null);
	let leaveOpen = $state(false);
	beforeNavigate((navigation) => {
		if (!guard || !dirty) return;
		if (navigation.willUnload || !navigation.to) {
			navigation.cancel();
			return;
		}
		navigation.cancel();
		leaveUrl = navigation.to.url;
		leaveOpen = true;
	});

	function leave() {
		guard = false;
		// eslint-disable-next-line svelte/no-navigation-without-resolve -- the URL comes from the navigation being resumed
		if (leaveUrl) void goto(leaveUrl);
	}
</script>

<form novalidate aria-label={labels.form} onsubmit={submit} class="flex flex-col gap-10">
	{#if banner}{@render banner()}{/if}
	<ErrorMessage {error} />

	<section class="flex flex-col gap-5">
		{#if mode === 'recipe'}
			<TextField
				label={labels.title}
				bind:value={draft.title}
				maxlength={150}
				error={errors.title ?? null}
			/>
			<TextArea
				label={labels.description}
				optional
				rows={3}
				bind:value={draft.description}
				error={errors.description ?? null}
			/>
			<div class="flex flex-col gap-2">
				<p class="text-sm font-semibold">{labels.cover}</p>
				{#if draft.cover}
					<img
						src={imageUrl(draft.cover.id, 'thumb')}
						alt={labels.coverAlt}
						class="max-h-56 w-full rounded-card border border-line object-cover"
					/>
				{/if}
				<div class="flex flex-wrap gap-2">
					<ImageUploadButton
						label={draft.cover ? labels.changeCover : labels.addCover}
						onUploaded={(image) => (draft.cover = image)}
					/>
					{#if draft.cover}
						<div>
							<Button variant="ghost" onclick={() => (draft.cover = null)}>
								{labels.removeCover}
							</Button>
						</div>
					{/if}
				</div>
			</div>
		{/if}
		<div class="grid grid-cols-2 gap-3">
			<NumberField
				label={labels.prepMinutes}
				optional
				suffix={labels.minutes}
				min={0}
				max={2880}
				bind:value={draft.prepMinutes}
				error={errors.prepMinutes ?? null}
			/>
			<NumberField
				label={labels.cookMinutes}
				optional
				suffix={labels.minutes}
				min={0}
				max={2880}
				bind:value={draft.cookMinutes}
				error={errors.cookMinutes ?? null}
			/>
		</div>
		{#if mode === 'recipe'}
			<TagInput
				label={labels.tags}
				hint={labels.tagsHint}
				bind:value={draft.tags}
				suggestions={tagSuggestions.map((s) => s.name)}
			/>
		{/if}
		<TagInput
			label={labels.utensils}
			hint={labels.utensilsHint}
			bind:value={draft.utensils}
			suggestions={utensilSuggestions.map((s) => s.name)}
		/>
		{#if visibility !== null}
			<VisibilitySelect bind:value={visibility} />
		{/if}
	</section>

	<section aria-labelledby="editor-ingredients">
		<h2 id="editor-ingredients" class="mb-4 text-2xl">{labels.sectionIngredients}</h2>
		<IngredientList bind:ingredients={draft.ingredients} {mode} {errors} />
	</section>

	<section aria-labelledby="editor-prep">
		<h2 id="editor-prep" class="mb-4 text-2xl">{labels.sectionPrep}</h2>
		<StepList
			bind:steps={draft.prepSteps}
			path="prepSteps"
			{mode}
			{errors}
			emptyText={t.recipes.steps.prepEmpty}
		/>
	</section>

	<section aria-labelledby="editor-instructions">
		<h2 id="editor-instructions" class="mb-4 text-2xl">{labels.sectionInstructions}</h2>
		<StepList
			bind:steps={draft.instructionSteps}
			path="instructionSteps"
			{mode}
			{errors}
			emptyText={t.recipes.steps.instructionsEmpty}
		/>
	</section>

	<ActionBar>
		<Button variant="secondary" href={cancelHref}>{labels.cancel}</Button>
		<Button type="submit" loading={saving}>{submitLabel}</Button>
	</ActionBar>
</form>

<ConfirmDialog
	bind:open={leaveOpen}
	title={labels.leaveTitle}
	description={labels.leaveText}
	confirmLabel={labels.leaveConfirm}
	onConfirm={leave}
/>
