<script lang="ts">
	import { createApi, unwrap } from '$lib/api/client';
	import { TextField } from '$lib/components/ui';
	import { t } from '$lib/i18n';
	import { cleanNickname, nicknameProblem, sameNickname, type NicknameProblem } from './nickname';

	/**
	 * The nickname input of onboarding and the profile: validates the rules of PROJECT §2 as you type
	 * and asks the server (debounced) whether the name is free. Your own nickname counts as free.
	 * `valid` tells the form whether it may submit; `error` shows a server refusal (e.g. taken).
	 */
	type Props = {
		value?: string;
		/** True when the value follows the rules and is known (or presumed) to be free. */
		valid?: boolean;
		/** The saved nickname, if any: it is never reported as taken. */
		current?: string | null;
		/** A refusal from the server, shown until the value changes. */
		error?: string | null;
		debounceMs?: number;
		onedit?: () => void;
	};

	let {
		value = $bindable(''),
		// `valid` is an output: the parent reads it through bind:.
		// eslint-disable-next-line no-useless-assignment
		valid = $bindable(),
		current = null,
		error = null,
		debounceMs = 350,
		onedit
	}: Props = $props();

	type Availability = 'idle' | 'checking' | 'available' | 'taken';
	/** The server's answer for one cleaned nickname; `available: null` = the lookup failed. */
	type Answer = { nickname: string; available: boolean | null };

	let answer = $state<Answer | null>(null);
	let touched = $state(false);

	let candidate = $derived(cleanNickname(value));
	let problem = $derived<NicknameProblem | null>(nicknameProblem(value));
	let own = $derived(current !== null && problem === null && sameNickname(value, current));

	let availability = $derived<Availability>(
		problem !== null
			? 'idle'
			: own
				? 'available'
				: answer?.nickname !== candidate
					? 'checking'
					: answer.available === null
						? 'idle' // The server checks again on save; a failed lookup does not block the form.
						: answer.available
							? 'available'
							: 'taken'
	);

	// A too-short name is only reported after the user leaves the field (typing "an" is not an error yet).
	let ruleError = $derived(
		problem === null || problem === 'empty' || (problem === 'tooShort' && !touched)
			? null
			: t.auth.nickname[problem]
	);
	let takenError = $derived(availability === 'taken' ? t.errors['nickname.taken'] : null);
	let shownError = $derived(ruleError ?? takenError ?? error);

	let hint = $derived(
		availability === 'checking'
			? t.auth.nickname.checking
			: availability === 'available' && !shownError
				? t.auth.nickname.available
				: t.auth.nickname.rules
	);

	$effect(() => {
		valid = problem === null && availability !== 'taken' && availability !== 'checking' && !error;
	});

	// Debounced lookup; a newer value aborts the older request.
	$effect(() => {
		if (problem !== null || own || answer?.nickname === candidate) return;
		const nickname = candidate;
		const controller = new AbortController();
		const timer = setTimeout(async () => {
			let available: boolean | null = null;
			try {
				const result = await unwrap(
					createApi({ onUnauthorized: null }).GET('/users/nickname-available', {
						params: { query: { nickname } },
						signal: controller.signal
					})
				);
				available = result.available;
			} catch {
				if (controller.signal.aborted) return;
			}
			answer = { nickname, available };
		}, debounceMs);
		return () => {
			clearTimeout(timer);
			controller.abort();
		};
	});
</script>

<TextField
	label={t.auth.nickname.label}
	bind:value
	{hint}
	error={shownError}
	autocomplete="nickname"
	autocapitalize="none"
	spellcheck={false}
	maxlength={60}
	oninput={() => onedit?.()}
	onblur={() => (touched = true)}
/>
<p class="sr-only" role="status">
	{#if availability === 'available' && !shownError}{t.auth.nickname.available}{/if}
</p>
