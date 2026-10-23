// The app-wide toast queue. Render <Toaster /> once (the root layout does); call `toast.*` anywhere.
// A module singleton is fine here: the app is a client-only SPA (TECHNICAL §4.1), so state never
// leaks between users.

export type ToastTone = 'neutral' | 'success' | 'error';

export type ToastAction = {
	/** Names what happens, e.g. `t.common.actions.undo`. */
	label: string;
	onAction: () => unknown;
};

export type ToastOptions = {
	tone?: ToastTone;
	action?: ToastAction;
	/** Milliseconds before it hides itself; `0` keeps it until dismissed. */
	duration?: number;
};

export type ToastItem = {
	id: number;
	message: string;
	tone: ToastTone;
	action?: ToastAction;
};

/** Plain toasts stay 5 s; a toast with an action (undo) stays 8 s so it can be reached. */
export const TOAST_DURATION = 5000;
export const TOAST_ACTION_DURATION = 8000;
const MAX_VISIBLE = 3;

type Timer = {
	handle: ReturnType<typeof setTimeout> | undefined;
	remaining: number;
	started: number;
};

export class ToastQueue {
	items = $state<ToastItem[]>([]);
	// Not reactive on purpose: timers never drive the UI.
	#timers: Record<number, Timer> = {};
	#nextId = 1;

	show(message: string, options: ToastOptions = {}): number {
		const id = this.#nextId++;
		const { tone = 'neutral', action } = options;
		const duration = options.duration ?? (action ? TOAST_ACTION_DURATION : TOAST_DURATION);
		this.items = [...this.items, { id, message, tone, action }].slice(-MAX_VISIBLE);
		for (const timerId of this.#ids()) {
			if (!this.items.some((item) => item.id === timerId)) this.#clear(timerId);
		}
		if (duration > 0) {
			this.#timers[id] = { handle: undefined, remaining: duration, started: 0 };
			this.resume(id);
		}
		return id;
	}

	success(message: string, options: Omit<ToastOptions, 'tone'> = {}): number {
		return this.show(message, { ...options, tone: 'success' });
	}

	error(message: string, options: Omit<ToastOptions, 'tone'> = {}): number {
		return this.show(message, { ...options, tone: 'error' });
	}

	dismiss(id: number): void {
		this.#clear(id);
		this.items = this.items.filter((item) => item.id !== id);
	}

	/** Runs the action once, then hides the toast. */
	async runAction(id: number): Promise<void> {
		const item = this.items.find((i) => i.id === id);
		if (!item?.action) return;
		this.dismiss(id);
		await item.action.onAction();
	}

	/** Stops the countdown (pointer over or focus inside the toast; WCAG 2.2.1). */
	pause(id: number): void {
		const timer = this.#timers[id];
		if (!timer || timer.handle === undefined) return;
		clearTimeout(timer.handle);
		timer.handle = undefined;
		timer.remaining -= Date.now() - timer.started;
	}

	resume(id: number): void {
		const timer = this.#timers[id];
		if (!timer || timer.handle !== undefined) return;
		timer.started = Date.now();
		timer.handle = setTimeout(() => this.dismiss(id), Math.max(timer.remaining, 0));
	}

	/** Pauses every toast (pointer over or focus inside the toast region). */
	pauseAll(): void {
		for (const id of this.#ids()) this.pause(id);
	}

	resumeAll(): void {
		for (const id of this.#ids()) this.resume(id);
	}

	clear(): void {
		for (const id of this.#ids()) this.#clear(id);
		this.items = [];
	}

	#ids(): number[] {
		return Object.keys(this.#timers).map(Number);
	}

	#clear(id: number) {
		const timer = this.#timers[id];
		if (timer?.handle !== undefined) clearTimeout(timer.handle);
		delete this.#timers[id];
	}
}

export const toast = new ToastQueue();
