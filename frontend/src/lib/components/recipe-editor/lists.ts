// Reordering rules shared by the ingredient and step lists (PROJECT §6.2).
// In a variant the rows of the base recipe keep their order; only new rows can pass them.

type Row = { base: boolean };

/** Whether the row at `index` may swap places with its neighbour in `direction`. */
export function canMove(rows: readonly Row[], index: number, direction: 'up' | 'down'): boolean {
	const other = direction === 'up' ? index - 1 : index + 1;
	const a = rows[index];
	const b = rows[other];
	if (!a || !b) return false;
	return !(a.base && b.base);
}

/** A new array with the row moved one place; unchanged when {@link canMove} says no. */
export function moveRow<T extends Row>(
	rows: readonly T[],
	index: number,
	direction: 'up' | 'down'
): T[] {
	const copy = [...rows];
	if (!canMove(rows, index, direction)) return copy;
	const other = direction === 'up' ? index - 1 : index + 1;
	const a = copy[index] as T;
	copy[index] = copy[other] as T;
	copy[other] = a;
	return copy;
}

/** Moves the row at `from` to position `to` (drag and drop); refused when it would reorder base rows. */
export function dropRow<T extends Row>(rows: readonly T[], from: number, to: number): T[] {
	if (from === to || !rows[from]) return [...rows];
	const moved = rows[from] as T;
	const without = rows.filter((_, i) => i !== from);
	const next = [...without.slice(0, to), moved, ...without.slice(to)];
	const order = (list: readonly T[]) => list.filter((r) => r.base);
	const before = order(rows);
	const after = order(next);
	return before.every((r, i) => r === after[i]) ? next : [...rows];
}
