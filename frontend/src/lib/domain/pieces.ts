// Small/medium/large piece combinations (PROJECT §5.4). Implemented in F1.
import { notImplemented } from './not-implemented';

export type PieceCombo =
	{ large: number; medium: number; small: number } | { fractionOfMedium: 0.25 | 0.5 };

export function bestPieceCombination(_t: number): PieceCombo {
	return notImplemented('bestPieceCombination');
}
