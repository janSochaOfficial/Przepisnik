<script lang="ts">
	import { imageUrl } from '$lib/components/recipe-editor/draft';
	import type { Step } from '$lib/domain/types';
	import { t } from '$lib/i18n';

	/** Numbered steps of a recipe: text paragraphs and photos (a thumbnail that grows to full size on tap). */
	type Props = { steps: Step[]; label: string };

	let { steps, label }: Props = $props();

	let expanded = $state<Record<string, boolean>>({});
</script>

<ol class="flex flex-col gap-5" aria-label={label}>
	{#each steps as step, n (step.id)}
		<li class="flex gap-3">
			<span
				class="tabular flex size-8 shrink-0 items-center justify-center rounded-full bg-cobalt-wash font-semibold"
				aria-hidden="true">{n + 1}</span
			>
			<div class="flex min-w-0 flex-1 flex-col gap-3">
				{#each step.blocks as block, b (b)}
					{#if block.type === 'text'}
						<p class="whitespace-pre-line">{block.text}</p>
					{:else}
						{@const key = `${step.id}:${b}`}
						<figure>
							<button
								type="button"
								class="block min-h-11 max-w-full rounded-control"
								aria-label={t.variants.view.showImage}
								aria-expanded={expanded[key] === true}
								onclick={() => (expanded[key] = !expanded[key])}
							>
								<img
									src={imageUrl(block.imageId, expanded[key] ? 'full' : 'thumb')}
									alt={block.caption ?? t.variants.view.imageAlt}
									loading="lazy"
									class={[
										'rounded-card border border-line object-cover',
										expanded[key] ? 'w-full' : 'max-h-40'
									]}
								/>
							</button>
							{#if block.caption}
								<figcaption class="mt-1 text-sm text-muted">{block.caption}</figcaption>
							{/if}
						</figure>
					{/if}
				{/each}
			</div>
		</li>
	{/each}
</ol>
