// See https://svelte.dev/docs/kit/types#app.d.ts
declare global {
	namespace App {
		interface Error {
			code?: string;
		}
		// interface Locals {}
		// interface PageData {}
		interface PageState {
			/** Variant editor seed passed by "create a variant from the current selection" (F5). */
			variantDraft?: import('$lib/components/recipe-editor/draft').RecipeDraft;
		}
		// interface Platform {}
	}
}

export {};
