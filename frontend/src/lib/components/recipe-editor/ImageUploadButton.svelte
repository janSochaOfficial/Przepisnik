<script lang="ts">
	import ImagePlus from '@lucide/svelte/icons/image-plus';
	import { ApiError } from '$lib/api/client';
	import { Button, ErrorMessage, type ButtonVariant } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { IMAGE_TYPES, uploadImage, type ImageInfo } from './upload';

	/**
	 * A button that opens the file picker, uploads the chosen photo (`POST /images`) and reports
	 * progress. `onPick` runs when the button is pressed, before the picker opens (the step editor
	 * remembers the cursor there).
	 */
	type Props = {
		label: string;
		variant?: ButtonVariant;
		onPick?: () => void;
		onUploaded: (image: ImageInfo) => void;
	};

	let { label, variant = 'secondary', onPick, onUploaded }: Props = $props();

	const uid = $props.id();
	const inputId = `upload-${uid}`;
	let percent = $state<number | null>(null);
	let error = $state<unknown>(null);

	async function handleFile(event: Event & { currentTarget: HTMLInputElement }) {
		const file = event.currentTarget.files?.[0];
		event.currentTarget.value = '';
		if (!file) return;
		error = null;
		percent = 0;
		try {
			onUploaded(await uploadImage(file, (p) => (percent = p)));
		} catch (e) {
			error = e instanceof ApiError ? e : new ApiError(0, 'server.error');
		} finally {
			percent = null;
		}
	}
</script>

<div class="flex flex-col gap-2">
	<input
		id={inputId}
		type="file"
		accept={IMAGE_TYPES.join(',')}
		class="hidden"
		onchange={handleFile}
	/>
	<div>
		<Button
			{variant}
			icon={ImagePlus}
			loading={percent !== null}
			onclick={() => {
				onPick?.();
				document.getElementById(inputId)?.click();
			}}
		>
			{label}
		</Button>
	</div>
	{#if percent !== null}
		<div class="flex items-center gap-3" role="status">
			<progress class="h-2 flex-1 accent-cobalt" max="100" value={percent}></progress>
			<span class="tabular text-sm text-muted">{t.recipes.steps.uploading(percent)}</span>
		</div>
	{/if}
	<ErrorMessage {error} />
</div>
