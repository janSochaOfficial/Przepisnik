// Google Identity Services, loaded on the login page only (the script is never part of other pages).
const SCRIPT_URL = 'https://accounts.google.com/gsi/client';

type Gis = {
	accounts: {
		id: {
			initialize(config: {
				client_id: string;
				callback: (response: { credential: string }) => void;
				ux_mode?: 'popup';
			}): void;
			renderButton(element: HTMLElement, options: Record<string, unknown>): void;
		};
	};
};

function gis(): Gis | undefined {
	return (window as unknown as { google?: Gis }).google;
}

let loading: Promise<Gis> | null = null;

/** Adds the GIS script once and resolves when `window.google` is ready. */
export function loadGoogleScript(): Promise<Gis> {
	const existing = gis();
	if (existing) return Promise.resolve(existing);
	loading ??= new Promise<Gis>((resolve, reject) => {
		const script = document.createElement('script');
		script.src = SCRIPT_URL;
		script.async = true;
		script.onload = () => {
			const loaded = gis();
			if (loaded) resolve(loaded);
			else reject(new Error('Google Identity Services did not initialise'));
		};
		script.onerror = () => reject(new Error('Google Identity Services failed to load'));
		document.head.append(script);
	}).catch((error: unknown) => {
		loading = null;
		document.querySelector(`script[src="${SCRIPT_URL}"]`)?.remove();
		throw error;
	});
	return loading;
}

/** Renders the official "Sign in with Google" button into `element`. */
export async function renderGoogleButton(
	element: HTMLElement,
	clientId: string,
	onCredential: (idToken: string) => void
): Promise<void> {
	const google = await loadGoogleScript();
	google.accounts.id.initialize({
		client_id: clientId,
		callback: (response) => onCredential(response.credential)
	});
	google.accounts.id.renderButton(element, {
		type: 'standard',
		theme: 'outline',
		size: 'large',
		text: 'signin_with',
		shape: 'rectangular',
		locale: 'pl',
		width: Math.min(320, Math.max(200, element.clientWidth || 280))
	});
}
