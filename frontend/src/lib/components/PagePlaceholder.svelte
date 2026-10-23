<script lang="ts">
	import { t } from '$lib/i18n';

	/** Temporary page body used by T00 until the owning feature task builds the screen. */
	let { title }: { title: string } = $props();
</script>

<h1 class="text-2xl font-semibold">{title}</h1>
<p class="text-muted mt-2">{t.common.underConstruction}</p>
