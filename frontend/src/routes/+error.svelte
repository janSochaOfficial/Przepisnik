<script lang="ts">
	import { page } from '$app/state';
	import { errorMessage, t } from '$lib/i18n';

	let message = $derived(
		errorMessage(page.error?.code ?? (page.status === 404 ? 'request.not_found' : 'server.error'))
	);
</script>

<h1 class="text-xl font-semibold">{t.common.errorTitle}</h1>
<p class="text-muted mt-2">{message}</p>
