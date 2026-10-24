<script lang="ts">
	import { goto } from '$app/navigation';
	import { resolve } from '$app/paths';
	import { ApiError } from '$lib/api/client';
	import { Button, Dialog, ErrorMessage, Field, toast } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { importRecipe } from './transfer';

	/**
	 * "Importuj": pick an export file (`.json`), `POST /recipes/import`, then open the new recipe
	 * (PROJECT §4.7). The catalog opens it with `?import=1`; the profile page can link there too.
	 */
	type Props = { open?: boolean };

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
	let busy = $state(false);

	async function submit(event?: SubmitEvent) {
		event?.preventDefault();
		if (!file || busy) return;
		busy = true;
		error = null;
		try {
			const recipe = await importRecipe(file);
			toast.success(t.recipes.transfer.imported);
			open = false;
			await goto(resolve('/przepisy/[id]', { id: recipe.id }));
		} catch (e) {
			error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			busy = false;
		}
	}
</script>

<Dialog
	bind:open
	title={t.recipes.transfer.importTitle}
	description={t.recipes.transfer.importText}
>
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
	</form>
	{#snippet footer()}
		<Button variant="secondary" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button type="submit" form={`import-${uid}`} loading={busy} disabled={!file}>
			{t.recipes.transfer.importSubmit}
		</Button>
	{/snippet}
</Dialog>
