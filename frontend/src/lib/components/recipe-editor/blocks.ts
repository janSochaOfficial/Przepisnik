// Pure operations on the content blocks of one recipe step (PROJECT §4.1): text and image blocks
// that sit one after another. The editor keeps every image flanked by text blocks (possibly empty)
// so the cursor always has somewhere to go; empty text blocks are dropped when the recipe is saved.
import type { ContentBlock } from '$lib/domain/types';

export type DraftBlock =
	| { key: string; type: 'text'; text: string }
	| { key: string; type: 'image'; imageId: string; caption: string };

let counter = 0;
/** A unique key for `{#each}` blocks; not sent to the server. */
export function newKey(prefix = 'k'): string {
	counter += 1;
	return `${prefix}${counter}`;
}

export function textBlock(text = ''): DraftBlock {
	return { key: newKey('b'), type: 'text', text };
}

export function imageBlock(imageId: string, caption = ''): DraftBlock {
	return { key: newKey('b'), type: 'image', imageId, caption };
}

function joinText(a: string, b: string): string {
	return a && b ? `${a}\n${b}` : a + b;
}

/** Merges neighbouring text blocks and makes sure text blocks flank every image. */
export function normalizeBlocks(blocks: readonly DraftBlock[]): DraftBlock[] {
	const out: DraftBlock[] = [];
	for (const block of blocks) {
		const last = out[out.length - 1];
		if (block.type === 'text' && last?.type === 'text') {
			out[out.length - 1] = { ...last, text: joinText(last.text, block.text) };
			continue;
		}
		if (block.type === 'image' && (last === undefined || last.type === 'image')) {
			out.push(textBlock());
		}
		out.push(block);
	}
	if (out.length === 0 || out[out.length - 1]?.type === 'image') out.push(textBlock());
	return out;
}

/**
 * Inserts an image into the text block at `index`, splitting the text at `cursor`
 * (a character offset, e.g. `textarea.selectionStart`).
 */
export function insertImageAt(
	blocks: readonly DraftBlock[],
	index: number,
	cursor: number,
	imageId: string
): DraftBlock[] {
	const target = blocks[index];
	if (target?.type !== 'text') return [...blocks];
	const at = Math.max(0, Math.min(cursor, target.text.length));
	const before = target.text.slice(0, at).replace(/\n$/, '');
	const after = target.text.slice(at).replace(/^\n/, '');
	return normalizeBlocks([
		...blocks.slice(0, index),
		{ ...target, text: before },
		imageBlock(imageId),
		textBlock(after),
		...blocks.slice(index + 1)
	]);
}

/** Removes a block; the text blocks around a removed image are joined again. */
export function removeBlock(blocks: readonly DraftBlock[], index: number): DraftBlock[] {
	return normalizeBlocks(blocks.filter((_, i) => i !== index));
}

function textAt(blocks: readonly DraftBlock[], index: number): string | null {
	const b = blocks[index];
	return b?.type === 'text' ? b.text : null;
}

/** An image moves up past the last line of the text above it (or past the whole block). */
export function canMoveImage(
	blocks: readonly DraftBlock[],
	index: number,
	direction: 'up' | 'down'
): boolean {
	const neighbour = textAt(blocks, direction === 'up' ? index - 1 : index + 1);
	return neighbour !== null && neighbour !== '';
}

export function moveImage(
	blocks: readonly DraftBlock[],
	index: number,
	direction: 'up' | 'down'
): DraftBlock[] {
	const image = blocks[index];
	if (image?.type !== 'image' || !canMoveImage(blocks, index, direction)) return [...blocks];
	const prev = blocks[index - 1];
	const next = blocks[index + 1];
	if (prev?.type !== 'text' || next?.type !== 'text') return [...blocks];
	if (direction === 'up') {
		const p = prev.text.lastIndexOf('\n');
		const head = p < 0 ? '' : prev.text.slice(0, p);
		const tail = p < 0 ? prev.text : prev.text.slice(p + 1);
		return normalizeBlocks([
			...blocks.slice(0, index - 1),
			{ ...prev, text: head },
			image,
			{ ...next, text: joinText(tail, next.text) },
			...blocks.slice(index + 2)
		]);
	}
	const q = next.text.indexOf('\n');
	const first = q < 0 ? next.text : next.text.slice(0, q);
	const rest = q < 0 ? '' : next.text.slice(q + 1);
	return normalizeBlocks([
		...blocks.slice(0, index - 1),
		{ ...prev, text: joinText(prev.text, first) },
		image,
		{ ...next, text: rest },
		...blocks.slice(index + 2)
	]);
}

/** Editor blocks from the API's blocks. */
export function fromContentBlocks(blocks: readonly ContentBlock[]): DraftBlock[] {
	return normalizeBlocks(
		blocks.map((b) =>
			b.type === 'text' ? textBlock(b.text) : imageBlock(b.imageId, b.caption ?? '')
		)
	);
}

/** The API's blocks: whitespace-only text is dropped, empty captions are omitted. */
export function toContentBlocks(blocks: readonly DraftBlock[]): ContentBlock[] {
	const out: ContentBlock[] = [];
	for (const b of blocks) {
		if (b.type === 'text') {
			if (b.text.trim() !== '') out.push({ type: 'text', text: b.text });
		} else {
			const caption = b.caption.trim();
			out.push(
				caption
					? { type: 'image', imageId: b.imageId, caption }
					: { type: 'image', imageId: b.imageId }
			);
		}
	}
	return out;
}
