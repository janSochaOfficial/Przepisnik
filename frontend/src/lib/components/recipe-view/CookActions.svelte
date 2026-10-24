<script lang="ts">
	import { ActionBar } from '$lib/components/shell';
	import { Button } from '$lib/components/ui';
	import { t } from '$lib/i18n';
</script>

<!--
	The sticky bottom bar of the recipe view. F6 replaces the two placeholder buttons with its
	deduction dialog and "add missing to the list" action; both read the current ingredients from
	`getCookContext()` (./cook-context.ts).
-->
<ActionBar label={t.variants.actions.group}>
	<Button variant="secondary" disabled>{t.variants.actions.addMissing}</Button>
	<Button disabled>{t.variants.actions.deduct}</Button>
</ActionBar>
