// Client-side variant auto-match (PROJECT §6.3, §6.6). Implemented in F2.
import { notImplemented } from './not-implemented';
import type {
	Ingredient,
	Pick,
	ProductPreference,
	ResolvedRecipe,
	SlotOption,
	VariantMatrix,
	VariantWrite
} from './types';

export type MatchInput = {
	matrix: VariantMatrix;
	picks: Record<string, Pick>;
	preferences: ProductPreference[];
};

export type RankedOption = {
	option: SlotOption;
	compatible: boolean;
	supportCount: number;
	selected: boolean;
	source?: 'user' | 'auto';
};

export type MatchResult = {
	/** A variant uuid or 'base'. */
	bestVariantId: string;
	isCustomMix: boolean;
	compatibleVariantIds: string[];
	/** Complete: user pins plus auto picks for every slot. */
	picks: Record<string, Pick>;
	/** Per slot, ordered for the option sheet. */
	options: Record<string, RankedOption[]>;
	/** Effective ingredient list in display order ("none" omitted). */
	ingredients: Ingredient[];
};

export function matchVariants(_input: MatchInput): MatchResult {
	return notImplemented('matchVariants');
}

export function pin(
	_picks: Record<string, Pick>,
	_slotKey: string,
	_optionKey: string
): Record<string, Pick> {
	return notImplemented('pin');
}

export function clearUserPicks(_picks: Record<string, Pick>): Record<string, Pick> {
	return notImplemented('clearUserPicks');
}

export function draftFromMatch(
	_stepsSource: ResolvedRecipe,
	_result: MatchResult
): VariantWrite['recipe'] {
	return notImplemented('draftFromMatch');
}
