<script lang="ts">
	import { resolve } from '$app/paths';
	import { Page } from '$lib/components/shell';
	import { Button, toast } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { importFormatExample } from '$lib/i18n/pl/import-format';

	// Opened from the import dialog in a new tab (PROJECT §4.7, D-40): static content, no data.
	const labels = t.importFormat;
	const exampleText = JSON.stringify(importFormatExample, null, 2);

	async function copyExample() {
		try {
			await navigator.clipboard.writeText(exampleText);
			toast.success(labels.copied);
		} catch {
			toast.error(labels.copyFailed);
		}
	}
</script>

<svelte:head>
	<title>{labels.title}</title>
</svelte:head>

<Page title={labels.title} back={resolve('/przepisy')} backLabel={labels.back}>
	<div class="flex flex-col gap-10">
		<p class="max-w-prose text-base text-muted">{labels.intro}</p>

		<section aria-labelledby="format-file" class="flex flex-col gap-3">
			<h2 id="format-file" class="font-serif text-2xl font-semibold">{labels.file.heading}</h2>
			<ul class="flex list-disc flex-col gap-2 pl-5">
				{#each labels.file.items as item (item)}
					<li>{item}</li>
				{/each}
			</ul>
		</section>

		<section aria-labelledby="format-structure" class="flex flex-col gap-4">
			<h2 id="format-structure" class="font-serif text-2xl font-semibold">
				{labels.structure.heading}
			</h2>
			<p>{labels.structure.intro}</p>
			{@render fieldTable(labels.structure.rows, 'format-structure')}

			{#each labels.objects as table (table.id)}
				<h3 id={table.id} class="pt-2 font-serif text-lg font-semibold">{table.heading}</h3>
				{@render fieldTable(table.rows, table.id)}
			{/each}

			<h3 id="format-units" class="pt-2 font-serif text-lg font-semibold">
				{labels.units.heading}
			</h3>
			<p>{labels.units.intro}</p>
			<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
			<div role="group" tabindex="0" aria-labelledby="format-units" class="overflow-x-auto">
				<table class="w-full min-w-[28rem] border-collapse text-left text-base">
					<thead>
						<tr class="border-b border-line-strong">
							<th scope="col" class="p-2 font-semibold">{labels.units.columns.value}</th>
							<th scope="col" class="p-2 font-semibold">{labels.units.columns.name}</th>
							<th scope="col" class="p-2 font-semibold">{labels.units.columns.conversion}</th>
						</tr>
					</thead>
					<tbody>
						{#each labels.units.rows as row (row.value)}
							<tr class="border-b border-line align-top">
								<td class="p-2"><code>{row.value}</code></td>
								<td class="p-2">{row.name}</td>
								<td class="p-2 tabular">{row.conversion}</td>
							</tr>
						{/each}
					</tbody>
				</table>
			</div>
		</section>

		<section aria-labelledby="format-matching" class="flex flex-col gap-3">
			<h2 id="format-matching" class="font-serif text-2xl font-semibold">
				{labels.matching.heading}
			</h2>
			<ul class="flex list-disc flex-col gap-2 pl-5">
				{#each labels.matching.items as item (item)}
					<li>{item}</li>
				{/each}
			</ul>
			<h3 class="pt-2 font-serif text-lg font-semibold">{labels.matching.duplicate.heading}</h3>
			<p>{labels.matching.duplicate.text}</p>
		</section>

		<section aria-labelledby="format-example" class="flex flex-col gap-3">
			<h2 id="format-example" class="font-serif text-2xl font-semibold">
				{labels.example.heading}
			</h2>
			<p>{labels.example.intro}</p>
			<div>
				<Button variant="secondary" onclick={copyExample}>{labels.copy}</Button>
			</div>
			<!-- A scrollable box must be focusable so keyboard users can scroll it (WCAG 2.1.1). -->
			<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
			<div
				role="region"
				tabindex="0"
				aria-label={labels.example.label}
				class="overflow-x-auto rounded-card border border-line bg-surface p-4 text-sm"
			>
				<pre><code>{exampleText}</code></pre>
			</div>
		</section>
	</div>
</Page>

{#snippet fieldTable(
	rows: { field: string; meaning: string; required: string; limits: string }[],
	headingId: string
)}
	<!-- svelte-ignore a11y_no_noninteractive_tabindex -->
	<div role="group" tabindex="0" aria-labelledby={headingId} class="overflow-x-auto">
		<table class="w-full min-w-[32rem] border-collapse text-left text-base">
			<thead>
				<tr class="border-b border-line-strong">
					<th scope="col" class="p-2 font-semibold">{labels.columns.field}</th>
					<th scope="col" class="p-2 font-semibold">{labels.columns.meaning}</th>
					<th scope="col" class="p-2 font-semibold">{labels.columns.required}</th>
					<th scope="col" class="p-2 font-semibold">{labels.columns.limits}</th>
				</tr>
			</thead>
			<tbody>
				{#each rows as row (row.field)}
					<tr class="border-b border-line align-top">
						<td class="p-2"><code>{row.field}</code></td>
						<td class="p-2">{row.meaning}</td>
						<td class="p-2">{row.required}</td>
						<td class="p-2">{row.limits}</td>
					</tr>
				{/each}
			</tbody>
		</table>
	</div>
{/snippet}
