// jsdom gaps that bits-ui (floating content, focus scope, media queries) relies on. Import this
// module first in component tests of UI primitives and of features that render them.
import { vi } from 'vitest';

class ResizeObserverStub {
	observe() {}
	unobserve() {}
	disconnect() {}
}

if (typeof window !== 'undefined') {
	window.ResizeObserver ??= ResizeObserverStub as unknown as typeof ResizeObserver;
	Element.prototype.scrollIntoView ??= function () {};
	Element.prototype.hasPointerCapture ??= () => false;
	Element.prototype.releasePointerCapture ??= () => {};
	Element.prototype.setPointerCapture ??= () => {};
	if (!window.matchMedia) setViewport('mobile');
	// jsdom has no layout, so `getClientRects()` is always empty and the focus trap (tabbable)
	// thinks nothing is visible. Report one rect for elements that are not `display: none`.
	const original = HTMLElement.prototype.getClientRects;
	HTMLElement.prototype.getClientRects = function (this: HTMLElement) {
		if (!this.isConnected || getComputedStyle(this).display === 'none') {
			return original.call(this);
		}
		const rect = this.getBoundingClientRect();
		return Object.assign([rect], { item: (i: number) => (i === 0 ? rect : null) });
	};
}

/**
 * Makes `matchMedia` answer as a phone (< 768 px, every `min-width` query false) or a desktop
 * (every `min-width` query true). Call before rendering.
 */
export function setViewport(kind: 'mobile' | 'desktop'): void {
	window.matchMedia = vi.fn((query: string) => ({
		matches: kind === 'desktop' && query.includes('min-width'),
		media: query,
		onchange: null,
		addEventListener: () => {},
		removeEventListener: () => {},
		addListener: () => {},
		removeListener: () => {},
		dispatchEvent: () => false
	})) as unknown as typeof window.matchMedia;
}
