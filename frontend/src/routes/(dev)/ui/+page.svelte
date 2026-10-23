<script lang="ts">
	import Plus from '@lucide/svelte/icons/plus';
	import Search from '@lucide/svelte/icons/search';
	import SlidersHorizontal from '@lucide/svelte/icons/sliders-horizontal';
	import Trash2 from '@lucide/svelte/icons/trash-2';
	import { resolve } from '$app/paths';
	import { ApiError } from '$lib/api/client';
	import { t } from '$lib/i18n';
	import { ActionBar, Page } from '$lib/components/shell';
	import {
		Avatar,
		BottomSheet,
		Button,
		Chip,
		Combobox,
		ConfirmDialog,
		Dialog,
		EmptyState,
		ErrorMessage,
		IconButton,
		NumberField,
		Select,
		Skeleton,
		Slider,
		Switch,
		Tabs,
		TagInput,
		TextArea,
		TextField,
		toast,
		type Option
	} from '$lib/components/ui';

	// Dev-only page: every primitive in every state (task F0). Texts come from t.common.showcase.
	const s = t.common.showcase;

	let name = $state(s.sampleTitle);
	let amount = $state<number | null>(1.5);
	let notes = $state(s.sampleText);
	let unit = $state('g');
	let product = $state<Option | null>(null);
	let tags = $state<string[]>([...s.chipSamples.slice(0, 2)]);
	let share = $state(true);
	let shareOff = $state(false);
	let remaining = $state(45);
	let tab = $state('all');
	let filters = $state<string[]>([s.chipSamples[0]]);
	let dialogOpen = $state(false);
	let confirmOpen = $state(false);
	let sheetOpen = $state(false);
	let sheetChoice = $state<string>(s.sheetOptions[0]);

	const units: Option[] = [
		{ value: 'g', label: s.units.g },
		{ value: 'ml', label: s.units.ml },
		{ value: 'pcs', label: s.units.pcs }
	];

	const products: Option[] = s.products.map((label, i) => ({ value: `p${i}`, label }));

	function searchProducts(query: string, signal: AbortSignal): Promise<Option[]> {
		return new Promise((resolve, reject) => {
			const timer = setTimeout(() => {
				const q = query.toLocaleLowerCase('pl');
				resolve(products.filter((p) => p.label.includes(q)));
			}, 300);
			signal.addEventListener('abort', () => {
				clearTimeout(timer);
				reject(new DOMException('aborted', 'AbortError'));
			});
		});
	}

	function toggleFilter(tag: string) {
		filters = filters.includes(tag) ? filters.filter((f) => f !== tag) : [...filters, tag];
	}

	function undoableDelete() {
		toast.show(s.toastDeleted, {
			action: { label: t.common.actions.undo, onAction: () => toast.success(s.toastRestored) }
		});
	}

	const sampleError = new ApiError(409, 'concurrency.conflict');
	const tabItems = [
		{ value: 'all', label: s.tabs.all, count: 128 },
		{ value: 'mine', label: s.tabs.mine, count: 12 },
		{ value: 'variants', label: s.tabs.variants }
	];
</script>

<Page title={s.title} subtitle={s.intro} back={resolve('/przepisy')}>
	{#snippet actions()}
		<IconButton icon={Search} label={t.common.ui.searchPlaceholder} />
	{/snippet}

	<div class="flex flex-col gap-10">
		<section aria-labelledby="sec-buttons" class="flex flex-col gap-4">
			<h2 id="sec-buttons" class="text-2xl">{s.sections.buttons}</h2>
			<div class="flex flex-wrap gap-2">
				<Button>{t.common.actions.save}</Button>
				<Button variant="secondary">{t.common.actions.edit}</Button>
				<Button variant="ghost">{t.common.actions.cancel}</Button>
				<Button variant="danger" icon={Trash2}>{t.common.actions.delete}</Button>
			</div>
			<div class="flex flex-wrap gap-2">
				<Button icon={Plus} size="lg">{t.common.actions.add}</Button>
				<Button loading>{s.loadingButton}</Button>
				<Button disabled>{s.disabled}</Button>
				<Button variant="secondary" disabled>{s.disabled}</Button>
			</div>
			<div class="flex flex-wrap items-center gap-2">
				<IconButton icon={Plus} label={t.common.actions.add} variant="primary" />
				<IconButton icon={SlidersHorizontal} label={s.filters} variant="secondary" />
				<IconButton icon={Search} label={t.common.ui.searchPlaceholder} />
				<IconButton icon={Trash2} label={t.common.actions.delete} variant="danger" />
				<IconButton icon={Plus} label={s.loadingButton} loading variant="secondary" />
			</div>
		</section>

		<section aria-labelledby="sec-fields" class="flex flex-col gap-5">
			<h2 id="sec-fields" class="text-2xl">{s.sections.fields}</h2>
			<TextField label={s.recipeName} bind:value={name} />
			<TextField label={s.withError} value={s.shortName} error={s.errorSample} />
			<div class="grid grid-cols-[1fr_auto] items-start gap-3">
				<NumberField label={s.amount} bind:value={amount} min={0} suffix={unit} />
				<Select label={s.unit} items={units} bind:value={unit} class="w-40" />
			</div>
			<NumberField label={s.amount} hint={s.disabled} disabled value={250} suffix="g" />
			<TextArea label={s.notes} bind:value={notes} optional />
			<Combobox
				label={s.ingredient}
				hint={s.ingredientHint}
				search={searchProducts}
				bind:value={product}
				onCreate={(q) => toast.success(s.created(q))}
			/>
			<TagInput label={s.tags} bind:value={tags} suggestions={[...s.chipSamples]} />
		</section>

		<section aria-labelledby="sec-choices" class="flex flex-col gap-5">
			<h2 id="sec-choices" class="text-2xl">{s.sections.choices}</h2>
			<div class="flex flex-wrap gap-2" role="group" aria-label={s.filters}>
				{#each s.chipSamples as tag (tag)}
					<Chip selected={filters.includes(tag)} onclick={() => toggleFilter(tag)}>{tag}</Chip>
				{/each}
				<Chip onclick={() => {}} disabled>{s.disabled}</Chip>
			</div>
			<div class="flex flex-wrap gap-2">
				<Chip>{s.chipSamples[1]}</Chip>
				<Chip selected>{s.chipSamples[2]}</Chip>
			</div>
			<div
				class="flex flex-col divide-y divide-line rounded-card border border-line bg-surface px-4"
			>
				<Switch label={s.share} description={s.shareHint} bind:checked={share} class="py-3" />
				<Switch label={s.share} bind:checked={shareOff} class="py-3" />
				<Switch label={s.disabled} disabled class="py-3" />
			</div>
			<Slider label={s.remaining} bind:value={remaining} />
			<Tabs items={tabItems} bind:value={tab} label={s.filters}>
				{#snippet panel(value)}
					<p class="text-muted">
						{s.tabPanel(tabItems.find((i) => i.value === value)?.label ?? '')}
					</p>
				{/snippet}
			</Tabs>
		</section>

		<section aria-labelledby="sec-feedback" class="flex flex-col gap-5">
			<h2 id="sec-feedback" class="text-2xl">{s.sections.feedback}</h2>
			<div class="flex flex-wrap gap-2">
				<Button variant="secondary" onclick={() => toast.success(s.toastSaved)}>
					{s.showToast}
				</Button>
				<Button variant="secondary" onclick={undoableDelete}>{s.showUndoToast}</Button>
			</div>
			<ErrorMessage error={sampleError} />
			<ErrorMessage error={new Error('boom')} variant="block" retry={() => undefined} />
			<div class="rounded-card border border-line bg-surface">
				<EmptyState title={s.emptyTitle} description={s.emptyText}>
					{#snippet action()}
						<Button icon={Plus}>{s.emptyAction}</Button>
					{/snippet}
				</EmptyState>
			</div>
			<div class="rounded-card border border-line bg-surface">
				<EmptyState icon={Search} title={t.common.ui.noResults} level={3} />
			</div>
			<div class="flex items-center gap-3" aria-busy="true">
				<Skeleton shape="circle" />
				<div class="flex flex-1 flex-col gap-2">
					<Skeleton class="w-2/3" />
					<Skeleton class="w-1/3" />
				</div>
				<span class="sr-only">{t.common.loading}</span>
			</div>
			<Skeleton shape="block" />
		</section>

		<section aria-labelledby="sec-overlays" class="flex flex-col gap-4">
			<h2 id="sec-overlays" class="text-2xl">{s.sections.overlays}</h2>
			<div class="flex flex-wrap gap-2">
				<Dialog bind:open={dialogOpen} title={s.dialogTitle} description={s.dialogText}>
					{#snippet trigger({ props })}
						<Button {...props} variant="secondary">{s.openDialog}</Button>
					{/snippet}
					<div class="flex flex-col gap-4">
						<NumberField label={s.amount} value={200} suffix="g" />
						<Slider label={s.remaining} value={60} />
					</div>
					{#snippet footer()}
						<Button variant="ghost" onclick={() => (dialogOpen = false)}>
							{t.common.actions.cancel}
						</Button>
						<Button onclick={() => (dialogOpen = false)}>{t.common.actions.confirm}</Button>
					{/snippet}
				</Dialog>

				<BottomSheet bind:open={sheetOpen} title={s.sheetTitle}>
					{#snippet trigger({ props })}
						<Button {...props} variant="secondary">{s.openSheet}</Button>
					{/snippet}
					<ul class="flex flex-col gap-1">
						{#each s.sheetOptions as option (option)}
							<li>
								<button
									type="button"
									aria-pressed={sheetChoice === option}
									class={[
										'flex min-h-12 w-full items-center rounded-control px-3 text-left text-base',
										sheetChoice === option
											? 'bg-cobalt-wash font-semibold text-ink'
											: 'hover:bg-sunken'
									]}
									onclick={() => {
										sheetChoice = option;
										sheetOpen = false;
									}}
								>
									{option}
								</button>
							</li>
						{/each}
					</ul>
				</BottomSheet>

				<ConfirmDialog
					bind:open={confirmOpen}
					title={s.confirmTitle}
					description={s.confirmText}
					confirmLabel={s.openConfirm}
					onConfirm={() => new Promise((r) => setTimeout(r, 600))}
				>
					{#snippet trigger({ props })}
						<Button {...props} variant="danger" icon={Trash2}>{s.openConfirm}</Button>
					{/snippet}
				</ConfirmDialog>
			</div>
		</section>

		<section aria-labelledby="sec-misc" class="flex flex-col gap-4">
			<h2 id="sec-misc" class="text-2xl">{s.sections.misc}</h2>
			<div class="flex items-center gap-3">
				<Avatar name={s.nicknames[0]} size="sm" />
				<Avatar name={s.nicknames[1]} />
				<Avatar name={s.nicknames[1]} size="lg" />
				<span class="flex items-center gap-2">
					<Avatar name={s.nicknames[0]} decorative />
					<span>{s.nicknames[0]}</span>
				</span>
			</div>
		</section>
	</div>

	<ActionBar>
		<Button variant="ghost">{t.common.actions.cancel}</Button>
		<Button>{t.common.actions.save}</Button>
	</ActionBar>
</Page>
