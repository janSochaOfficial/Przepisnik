<script lang="ts">
	import { untrack } from 'svelte';
	import {
		Button,
		Dialog,
		ErrorMessage,
		NumberField,
		Select,
		Switch,
		TextField,
		toast
	} from '$lib/components/ui';
	import type {
		BuiltInUnit,
		CustomUnitRef,
		CustomUnitWrite,
		PluralForms,
		ProductRef
	} from '$lib/domain/types';
	import { suggestPluralForms } from '$lib/domain/plural';
	import { t } from '$lib/i18n';
	import { compatibleBuiltIns } from './options';
	import { createUnit } from './units-client';

	/**
	 * Mount it only while open (`{#if}`), so every use starts with a fresh form.
	 * "+ Nowa jednostka…" (PROJECT §5.6): a name, its four plural forms (guessed from the name until
	 * the user edits them), "Tylko dla produktu <nazwa>" (on by default when there is a product) and
	 * "1 <unit> = [value] [built-in unit]". An existing unit with the same name and scope is reused.
	 */
	type Props = {
		open?: boolean;
		product: ProductRef | null;
		onCreated: (unit: CustomUnitRef) => void;
	};

	let { open = $bindable(false), product, onCreated }: Props = $props();

	const labels = t.customUnits.dialog;
	const unitLabels = t.recipes.ingredients.unitLabels;
	const MAX = 40;
	const FORM_KEYS = ['one', 'few', 'many', 'fraction'] as const;

	let name = $state('');
	let forms = $state<PluralForms>({ one: '', few: '', many: '', fraction: '' });
	let formsEdited = $state(false);
	let onlyForProduct = $state(untrack(() => product !== null));
	let value = $state<number | null>(null);
	let equalsUnit = $state<string>(untrack(() => initialUnit(product)));
	let saving = $state(false);
	let submitted = $state(false);
	let error = $state<unknown>(null);

	function initialUnit(p: ProductRef | null): BuiltInUnit {
		if (!p) return 'g';
		return p.measureKind === 'volume' ? 'ml' : p.measureKind === 'count' ? 'piece' : 'g';
	}

	let scopeProduct = $derived(onlyForProduct ? product : null);
	let unitItems = $derived(
		compatibleBuiltIns(scopeProduct).map((u) => ({ value: u, label: unitLabels[u] }))
	);

	function nameChanged(next: string) {
		name = next;
		if (!formsEdited) forms = suggestPluralForms(next);
	}

	function editForm(key: (typeof FORM_KEYS)[number], next: string) {
		formsEdited = true;
		forms = { ...forms, [key]: next };
	}

	function scopeChanged(next: boolean) {
		onlyForProduct = next;
		const allowed = compatibleBuiltIns(next ? product : null);
		if (!allowed.includes(equalsUnit as BuiltInUnit)) equalsUnit = allowed[0] ?? 'g';
	}

	function lengthError(text: string): string | null {
		if (!submitted && text.trim().length === 0) return null;
		if (text.trim().length === 0) return labels.required;
		return text.trim().length > MAX ? labels.tooLong : null;
	}

	let nameError = $derived(lengthError(name));
	let formErrors = $derived(Object.fromEntries(FORM_KEYS.map((k) => [k, lengthError(forms[k])])));
	let valueError = $derived(
		submitted && !(value !== null && value > 0) ? labels.valueRequired : null
	);
	let example = $derived(
		labels.example({
			one: forms.one || '…',
			few: forms.few || '…',
			many: forms.many || '…',
			fraction: forms.fraction || '…'
		})
	);

	async function submit(event?: SubmitEvent) {
		event?.preventDefault();
		submitted = true;
		if (nameError || valueError || Object.values(formErrors).some((e) => e !== null)) return;
		if (value === null) return;
		saving = true;
		error = null;
		const body: CustomUnitWrite = {
			name: name.trim(),
			forms: {
				one: forms.one.trim(),
				few: forms.few.trim(),
				many: forms.many.trim(),
				fraction: forms.fraction.trim()
			},
			productId: scopeProduct?.id ?? null,
			equals: { value, unit: equalsUnit as BuiltInUnit }
		};
		try {
			const unit = await createUnit(body);
			// An existing unit with the same name and scope comes back instead; it is used the same way.
			toast.success(labels.created(unit.name));
			open = false;
			onCreated(unit);
		} catch (e) {
			error = e;
		} finally {
			saving = false;
		}
	}
</script>

<Dialog bind:open title={labels.title}>
	<form id="new-unit-form" class="flex flex-col gap-5" onsubmit={submit}>
		<ErrorMessage {error} />
		<TextField
			label={labels.name}
			placeholder={labels.namePlaceholder}
			value={name}
			oninput={(e) => nameChanged(e.currentTarget.value)}
			error={nameError}
			maxlength={MAX}
		/>
		<fieldset class="flex flex-col gap-3">
			<legend class="font-semibold">{labels.formsHeading}</legend>
			<p class="text-sm text-muted">{labels.formsHint}</p>
			<div class="grid grid-cols-2 gap-3">
				{#each FORM_KEYS as key (key)}
					<TextField
						label={labels.forms[key]}
						value={forms[key]}
						oninput={(e) => editForm(key, e.currentTarget.value)}
						error={formErrors[key] ?? null}
						maxlength={MAX}
					/>
				{/each}
			</div>
			<p class="text-sm text-muted" data-testid="unit-example">{example}</p>
		</fieldset>
		{#if product}
			<Switch
				label={labels.onlyFor(product.name)}
				description={labels.onlyForHint}
				checked={onlyForProduct}
				onCheckedChange={scopeChanged}
			/>
		{/if}
		<div class="flex flex-col gap-2">
			<p class="font-semibold">{labels.equals(name.trim())}</p>
			<div class="grid grid-cols-[1fr_1fr] gap-3">
				<NumberField label={labels.value} bind:value min={0} error={valueError} />
				<Select label={labels.unit} items={unitItems} bind:value={equalsUnit} />
			</div>
		</div>
	</form>
	{#snippet footer()}
		<Button variant="secondary" onclick={() => (open = false)}>{t.common.actions.cancel}</Button>
		<Button type="submit" form="new-unit-form" loading={saving}>{labels.submit}</Button>
	{/snippet}
</Dialog>
