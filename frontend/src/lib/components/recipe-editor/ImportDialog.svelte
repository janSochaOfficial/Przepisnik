<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { ApiError } from '$lib/api/client';
	import type { components } from '$lib/api/schema';
	import { Button, Dialog, ErrorMessage, Field, toast } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import {
		findImportMatches,
		importAsVariants,
		importRecipe,
		matchTitle,
		readExportFile
	} from './transfer';

	/**
	 * "Importuj": pick an export file (`.json`) and import it (PROJECT §4.7). The file is parsed
	 * locally first; when the caller can already see recipes with the same title, a choice step
	 * (`t.recipes.transfer.existsTitle`) offers adding the file as variants of one of them (D-35), a normal
	 * import, or cancelling. The catalog opens the dialog with `?import=1`.
	 */
	type Props = { open?: boolean };
	type RecipeExport = components['schemas']['RecipeExport'];
	type RecipeSummary = components['schemas']['RecipeSummary'];
	type Choice = { body: RecipeExport; matches: RecipeSummary[] };

	let { open = $bindable(false) }: Props = $props();

	const uid = $props.id();
	// Writable deriveds start over every time the dialog opens or closes.
	let file = $derived.by((): File | null => {
		void open;
		return null;
	});
	let error = $derived.by((): unknown => {
		void open;
		return null;
	});
	let choice = $derived.by((): Choice | null => {
		void open;
		return null;
	});
	// What runs: `'file'` (reading and matching), `'new'` (normal import from the choice step) or a target recipe id.
	let busy = $state<string | null>(null);

	function fail(e: unknown) {
		error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
	}

	async function submit(event?: SubmitEvent) {
		event?.preventDefault();
		if (!file || busy) return;
		busy = 'file';
		error = null;
		try {
			const body = await readExportFile(file);
			const title = matchTitle(body);
			const matches = title ? await findImportMatches(title) : [];
			if (matches.length > 0) {
				choice = { body, matches };
				return;
			}
			await importAsNew(body);
		} catch (e) {
			fail(e);
		} finally {
			busy = null;
		}
	}

	async function importAsNew(body: RecipeExport) {
		const recipe = await importRecipe(body);
		toast.success(t.recipes.transfer.imported);
		open = false;
		await goto(resolve('/przepisy/[id]', { id: recipe.id }));
	}

	async function chooseNew() {
		if (!choice || busy) return;
		busy = 'new';
		error = null;
		try {
			await importAsNew(choice.body);
		} catch (e) {
			fail(e);
		} finally {
			busy = null;
		}
	}

	async function addAsVariant(match: RecipeSummary) {
		if (!choice || busy) return;
		busy = match.id;
		error = null;
		try {
			const result = await importAsVariants(match.id, choice.body);
			const first = result.variants[0];
			if (!first) {
				toast.show(t.recipes.transfer.allSkipped(result.skipped));
				return;
			}
			toast.success(
				result.skipped > 0
					? t.recipes.transfer.importedAsVariantsSkipped(result.skipped)
					: t.recipes.transfer.importedAsVariants
			);
			open = false;
			const recipe = resolve('/przepisy/[id]', { id: result.recipeId });
			// eslint-disable-next-line svelte/no-navigation-without-resolve -- a resolved path plus a query string
			await goto(`${recipe}?wariant=${first.id}`);
		} catch (e) {
			fail(e);
		} finally {
			busy = null;
		}
	}
</script>

<Dialog
	bind:open
	title={choice ? t.recipes.transfer.existsTitle : t.recipes.transfer.importTitle}
	description={choice ? t.recipes.transfer.existsText : t.recipes.transfer.importText}
>
	{#if choice}
		<div class="flex flex-col gap-4">
			<ErrorMessage {error} />
			<ul class="flex flex-col gap-2" aria-label={t.recipes.transfer.matchesLabel}>
				{#each choice.matches as match (match.id)}
					<li
						class="flex flex-col gap-3 rounded-control border border-line-strong bg-surface p-3 sm:flex-row sm:items-center sm:justify-between"
					>
						<div class="min-w-0">
							<p id={`match-${uid}-${match.id}`} class="font-semibold break-words text-ink">
								{match.title}
							</p>
							<p class="text-sm text-muted">
								{t.recipes.transfer.matchAuthor(match.author.nickname)} · {t.recipes.visibility[
									match.visibility
								]}
							</p>
						</div>
						<Button
							variant="secondary"
							aria-describedby={`match-${uid}-${match.id}`}
							loading={busy === match.id}
							disabled={busy !== null && busy !== match.id}
							onclick={() => addAsVariant(match)}
						>
							{t.recipes.transfer.addAsVariant}
						</Button>
					</li>
				{/each}
			</ul>
		</div>
	{:else}
		<form id={`import-${uid}`} class="flex flex-col gap-4" onsubmit={submit}>
			<ErrorMessage {error} />
			<Field label={t.recipes.transfer.importFile} id={`import-file-${uid}`}>
				{#snippet control({ id, describedBy })}
					<input
						{id}
						type="file"
						accept=".json,application/json"
						aria-describedby={describedBy}
						class="min-h-12 w-full rounded-control border border-line-strong bg-surface p-2 text-base file:mr-3 file:min-h-9 file:rounded-control file:border-0 file:bg-cobalt-wash file:px-3 file:font-semibold file:text-ink"
						onchange={(e) => (file = e.currentTarget.files?.[0] ?? null)}
					/>
				{/snippet}
			</Field>
			<a
				href={resolve('/przepisy/import/format')}
				target="_blank"
				rel="noopener"
				class="inline-flex min-h-11 items-center self-start font-semibold text-cobalt underline underline-offset-4"
			>
				{t.recipes.transfer.formatHelp}
			</a>
		</form>
	{/if}
	{#snippet footer()}
		<Button variant="secondary" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		{#if choice}
			<Button
				loading={busy === 'new'}
				disabled={busy !== null && busy !== 'new'}
				onclick={chooseNew}
			>
				{t.recipes.transfer.importAsNew}
			</Button>
		{:else}
			<Button type="submit" form={`import-${uid}`} loading={busy === 'file'} disabled={!file}>
				{t.recipes.transfer.importSubmit}
			</Button>
		{/if}
	{/snippet}
</Dialog>
