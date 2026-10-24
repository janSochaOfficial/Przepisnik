<script lang="ts">
	import { createApi, unwrap } from '$lib/api/client';
	import { Button, Dialog, ErrorMessage, TextField, toast } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import type { FridgeSummary } from './types';

	/** Asks for a name and creates a fridge. Mount it only while open. */
	type Props = {
		open?: boolean;
		onCreated: (fridge: FridgeSummary) => void;
	};

	let { open = $bindable(false), onCreated }: Props = $props();

	const labels = t.fridge.switcher;

	let name = $state('');
	let saving = $state(false);
	let error = $state<unknown>(null);

	async function create(event: SubmitEvent) {
		event.preventDefault();
		if (!name.trim() || saving) return;
		saving = true;
		error = null;
		try {
			const fridge = await unwrap(createApi().POST('/fridges', { body: { name: name.trim() } }));
			open = false;
			toast.success(labels.created);
			onCreated(fridge);
		} catch (e) {
			error = e;
		} finally {
			saving = false;
		}
	}
</script>

<Dialog bind:open title={labels.newTitle}>
	<form id="new-fridge-form" class="flex flex-col gap-4" onsubmit={create}>
		<ErrorMessage {error} />
		<TextField label={labels.nameLabel} bind:value={name} maxlength={80} />
	</form>
	{#snippet footer()}
		<Button variant="ghost" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button type="submit" form="new-fridge-form" loading={saving} disabled={!name.trim()}>
			{labels.create}
		</Button>
	{/snippet}
</Dialog>
