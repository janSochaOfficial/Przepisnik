// Small/medium/large piece combinations (PROJECT §5.4).
import { SIZE_FACTOR } from './units';

export type PieceCombo =
	{ large: number; medium: number; small: number } | { fractionOfMedium: 0.25 | 0.5 };

const TOLERANCE = 0.1;
const EPS = 1e-9;

type Candidate = { large: number; medium: number; small: number; error: number };

// Better = smaller error, then more medium pieces.
function better(a: Candidate, b: Candidate): boolean {
	return a.error < b.error - EPS || (Math.abs(a.error - b.error) <= EPS && a.medium > b.medium);
}

/**
 * The combination of large/medium/small pieces for `t` medium-equivalents.
 * t < 0.3 → ¼ of a medium, t < 0.625 → ½. Otherwise the fewest pieces within ±10 % of `t`;
 * ties go to the smaller error, then to more medium pieces. When nothing is within ±10 %,
 * the smallest error wins, searching up to ceil(t / 0.75) + 1 pieces.
 */
export function bestPieceCombination(t: number): PieceCombo {
	if (!(t >= 0.3)) return { fractionOfMedium: 0.25 };
	if (t < 0.625) return { fractionOfMedium: 0.5 };

	const maxPieces = Math.ceil(t / SIZE_FACTOR.small - EPS) + 1;
	let bestOverall: Candidate | null = null;
	for (let n = 1; n <= maxPieces; n++) {
		let bestInside: Candidate | null = null;
		for (let large = 0; large <= n; large++) {
			for (let medium = 0; medium <= n - large; medium++) {
				const small = n - large - medium;
				const sum =
					large * SIZE_FACTOR.large + medium * SIZE_FACTOR.medium + small * SIZE_FACTOR.small;
				const c: Candidate = { large, medium, small, error: Math.abs(sum - t) };
				if (c.error <= TOLERANCE * t + EPS && (bestInside === null || better(c, bestInside))) {
					bestInside = c;
				}
				if (bestOverall === null || better(c, bestOverall)) bestOverall = c;
			}
		}
		if (bestInside !== null) return pick(bestInside);
	}
	return pick(bestOverall!);
}

function pick(c: Candidate): PieceCombo {
	return { large: c.large, medium: c.medium, small: c.small };
}
