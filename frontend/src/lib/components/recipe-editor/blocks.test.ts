import { describe, expect, it } from 'vitest';
import {
	canMoveImage,
	fromContentBlocks,
	imageBlock,
	insertImageAt,
	moveImage,
	normalizeBlocks,
	removeBlock,
	textBlock,
	toContentBlocks,
	type DraftBlock
} from './blocks';

const shape = (blocks: DraftBlock[]) =>
	blocks.map((b) => (b.type === 'text' ? `T:${b.text}` : `I:${b.imageId}`));

describe('insertImageAt', () => {
	it('splits the text block at the cursor', () => {
		const out = insertImageAt([textBlock('Pierwszy akapit.\nDrugi akapit.')], 0, 16, 'img1');
		expect(shape(out)).toEqual(['T:Pierwszy akapit.', 'I:img1', 'T:Drugi akapit.']);
	});

	it('keeps an empty text block after an image inserted at the end', () => {
		const out = insertImageAt([textBlock('abc')], 0, 3, 'img1');
		expect(shape(out)).toEqual(['T:abc', 'I:img1', 'T:']);
	});

	it('ignores a non-text target', () => {
		const blocks = normalizeBlocks([imageBlock('a')]);
		expect(shape(insertImageAt(blocks, 1, 0, 'b'))).toEqual(shape(blocks));
	});
});

describe('removeBlock', () => {
	it('joins the text around a removed image', () => {
		const blocks = [textBlock('przed'), imageBlock('a'), textBlock('po')];
		expect(shape(removeBlock(blocks, 1))).toEqual(['T:przed\npo']);
	});
});

describe('normalizeBlocks', () => {
	it('separates two images with a text block and flanks them', () => {
		expect(shape(normalizeBlocks([imageBlock('a'), imageBlock('b')]))).toEqual([
			'T:',
			'I:a',
			'T:',
			'I:b',
			'T:'
		]);
	});

	it('returns one empty text block for no blocks', () => {
		expect(shape(normalizeBlocks([]))).toEqual(['T:']);
	});
});

describe('moveImage', () => {
	const blocks = () => [textBlock('a\nb'), imageBlock('x'), textBlock('c\nd')];

	it('moves up past the last line of the text above', () => {
		expect(shape(moveImage(blocks(), 1, 'up'))).toEqual(['T:a', 'I:x', 'T:b\nc\nd']);
	});

	it('moves down past the first line of the text below', () => {
		expect(shape(moveImage(blocks(), 1, 'down'))).toEqual(['T:a\nb\nc', 'I:x', 'T:d']);
	});

	it('cannot move past nothing', () => {
		const start = [textBlock(''), imageBlock('x'), textBlock('z')];
		expect(canMoveImage(start, 1, 'up')).toBe(false);
		expect(shape(moveImage(start, 1, 'up'))).toEqual(['T:', 'I:x', 'T:z']);
	});
});

describe('content block conversion', () => {
	it('drops blank text and empty captions when writing', () => {
		const out = toContentBlocks([textBlock('  '), imageBlock('a', ' '), textBlock('x')]);
		expect(out).toEqual([
			{ type: 'image', imageId: 'a' },
			{ type: 'text', text: 'x' }
		]);
	});

	it('round-trips the API blocks', () => {
		const api = [
			{ type: 'text', text: 'one' },
			{ type: 'image', imageId: 'a', caption: 'cap' },
			{ type: 'text', text: 'two' }
		] as const;
		expect(toContentBlocks(fromContentBlocks([...api]))).toEqual(api);
	});
});
