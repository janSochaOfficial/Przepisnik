// Feature fixtures (owned by F4): products, tags, utensils and image upload. Typed with Schemas
// from ./types; export MockHandlers for mockApi().
import type { MockHandlers } from './index';
import type { Schemas } from './types';

const product = (
	id: string,
	name: string,
	over: Partial<Schemas['ProductRef']> = {}
): Schemas['ProductRef'] => ({
	id,
	name,
	category: 'other',
	measureKind: 'mass',
	displayClass: 'weight',
	hasSizes: false,
	allowHalf: false,
	gramsPerMl: null,
	gramsPerPiece: null,
	defaultPackageAmount: null,
	pieceForms: null,
	...over
});

/** Flour: mass only (no density), so only g and kg make sense. */
export const flour = product('00000000-0000-7000-8000-00000000a001', 'mąka pszenna', {
	category: 'dryGoods'
});
/** Milk: a liquid with a density, so volume and mass units are both fine. */
export const milk = product('00000000-0000-7000-8000-00000000a002', 'mleko', {
	category: 'dairy',
	measureKind: 'volume',
	displayClass: 'liquid',
	gramsPerMl: 1.03
});
/** Onion: a countable product with sizes. */
export const onion = product('00000000-0000-7000-8000-00000000a003', 'cebula', {
	category: 'vegetables',
	measureKind: 'count',
	displayClass: 'piece',
	hasSizes: true
});
/** What `POST /products` answers for the product created inline in the tests. */
export const cardamom = product('00000000-0000-7000-8000-00000000a004', 'kardamon', {
	category: 'spices',
	measureKind: 'volume',
	displayClass: 'spice'
});

export const products = [flour, milk, onion];

export const tagObiad: Schemas['TagRef'] = {
	id: '00000000-0000-7000-8000-00000000b001',
	name: 'obiad',
	isSystem: false
};
export const tagQuick: Schemas['TagRef'] = {
	id: '00000000-0000-7000-8000-00000000b002',
	name: 'szybkie',
	isSystem: true
};
export const tags = [tagObiad, tagQuick];

export const utensilOven: Schemas['UtensilRef'] = {
	id: '00000000-0000-7000-8000-00000000c001',
	name: 'piekarnik'
};

export const uploadedImage: Schemas['ImageInfo'] = {
	id: '00000000-0000-7000-8000-00000000d001',
	url: '/api/v1/images/00000000-0000-7000-8000-00000000d001',
	thumbnailUrl: '/api/v1/images/00000000-0000-7000-8000-00000000d001?size=thumb',
	width: 800,
	height: 600
};

/** A 1×1 transparent PNG, enough for `<img>` and for the file picker. */
export const tinyPng = Buffer.from(
	'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==',
	'base64'
);

export const catalogHandlers: MockHandlers = {
	'GET /products': ({ url }) => {
		const q = (url.searchParams.get('q') ?? '').toLocaleLowerCase('pl');
		return { body: products.filter((p) => p.name.toLocaleLowerCase('pl').includes(q)) };
	},
	'GET /products/:id': ({ params }) => {
		const found = [...products, cardamom].find((p) => p.id === params.id);
		return found
			? { body: found }
			: { status: 404, body: { status: 404, code: 'product.not_found' } };
	},
	'POST /products': { status: 201, body: cardamom },
	'GET /tags': { body: tags },
	'POST /tags': ({ json }) => ({
		status: 201,
		body: {
			id: '00000000-0000-7000-8000-00000000b0ff',
			name: (json as { name: string }).name,
			isSystem: false
		}
	}),
	'GET /utensils': { body: [utensilOven] },
	'POST /utensils': ({ json }) => ({
		status: 201,
		body: { id: '00000000-0000-7000-8000-00000000c0ff', name: (json as { name: string }).name }
	}),
	'POST /images': { status: 201, body: uploadedImage }
};
