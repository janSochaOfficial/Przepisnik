import type { ButtonSize, ButtonVariant } from './types';

// Shared class lists, so Button, IconButton and links styled as buttons look identical.

const base =
	'inline-flex items-center justify-center gap-2 font-sans font-semibold select-none ' +
	'rounded-control border transition-colors duration-150 ' +
	'disabled:cursor-not-allowed disabled:opacity-55 aria-busy:cursor-progress';

const variants: Record<ButtonVariant, string> = {
	primary:
		'bg-cobalt text-on-cobalt border-cobalt hover:bg-cobalt-strong hover:border-cobalt-strong',
	secondary: 'bg-surface text-cobalt border-line-strong hover:bg-cobalt-wash',
	ghost: 'bg-transparent text-cobalt border-transparent hover:bg-cobalt-wash',
	danger: 'bg-beet text-on-beet border-beet hover:bg-beet-strong hover:border-beet-strong'
};

const sizes: Record<ButtonSize, string> = {
	md: 'min-h-11 px-4 text-base',
	lg: 'min-h-13 px-6 text-lg'
};

const iconSizes: Record<ButtonSize, string> = {
	md: 'size-11',
	lg: 'size-13'
};

export function buttonClass(variant: ButtonVariant, size: ButtonSize, fullWidth = false): string {
	return [base, variants[variant], sizes[size], fullWidth ? 'w-full' : ''].join(' ');
}

export function iconButtonClass(variant: ButtonVariant, size: ButtonSize): string {
	return [base, variants[variant], iconSizes[size], 'p-0'].join(' ');
}

/** The box of a text-like control (TextField, NumberField, TextArea, Select and Combobox triggers). */
export const controlBox =
	'w-full min-h-12 rounded-control border border-line-strong bg-surface px-3 text-base text-ink ' +
	'placeholder:text-muted transition-colors duration-150 ' +
	'hover:border-ink focus-within:border-cobalt ' +
	'aria-invalid:border-beet aria-invalid:border-2 ' +
	'disabled:cursor-not-allowed disabled:bg-sunken disabled:opacity-70';
