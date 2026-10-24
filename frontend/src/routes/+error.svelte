<script lang="ts">
	import { page } from '$app/state';
	import { Page } from '$lib/components/shell';
	import { errorMessage, t } from '$lib/i18n';

	let message = $derived(
		errorMessage(page.error?.code ?? (page.status === 404 ? 'request.not_found' : 'server.error'))
	);
</script>

<Page title={t.common.errorTitle}>
	<p class="text-muted">{message}</p>
</Page>
