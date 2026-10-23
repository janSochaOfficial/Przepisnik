import type { LucideIcon } from '@lucide/svelte';

/** A Lucide icon component, e.g. `import Plus from '@lucide/svelte/icons/plus'`. */
export type IconComponent = LucideIcon;

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';

/** `md` = 44 px (default, lists and toolbars), `lg` = 52 px (the one main action of a screen). */
export type ButtonSize = 'md' | 'lg';

/** An option of `Select` and `Combobox`; `hint` is a second, muted line. */
export type Option = { value: string; label: string; hint?: string; disabled?: boolean };

/** A tab of `Tabs`; `count` shows a small number badge. */
export type TabItem = { value: string; label: string; count?: number };
