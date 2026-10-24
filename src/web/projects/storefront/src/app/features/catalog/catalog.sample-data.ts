import { ProductSize, ProductSummary } from '../../shared/models/product.model';

/**
 * Placeholder catalogue used until the storefront is wired to the catalogue API.
 * Replace with the API-backed store when the catalogue feature lands.
 */
export const SAMPLE_PRODUCTS: readonly ProductSummary[] = [
  {
    slug: 'floral-summer-dress',
    name: 'The Floral Summer Dress',
    price: 5250,
    category: 'girls',
    type: 'dresses',
  },
  {
    slug: 'linen-shirt-dress',
    name: 'The Linen Shirt Dress',
    price: 5450,
    category: 'girls',
    type: 'dresses',
  },
  {
    slug: 'knit-cardigan',
    name: 'The Knit Cardigan',
    price: 4450,
    category: 'girls',
    type: 'knitwear',
  },
  {
    slug: 'pleated-skirt',
    name: 'The Pleated Skirt',
    price: 3650,
    category: 'girls',
    type: 'bottoms',
  },
  {
    slug: 'corduroy-pinafore',
    name: 'The Corduroy Pinafore',
    price: 4750,
    category: 'girls',
    type: 'dresses',
  },
  {
    slug: 'poplin-blouse',
    name: 'The Poplin Blouse',
    price: 3250,
    category: 'girls',
    type: 'tops',
  },
  {
    slug: 'wide-leg-trouser',
    name: 'The Wide-Leg Trouser',
    price: 3950,
    category: 'girls',
    type: 'bottoms',
  },
  {
    slug: 'quilted-jacket',
    name: 'The Quilted Jacket',
    price: 6950,
    category: 'girls',
    type: 'outerwear',
  },
];

export const SAMPLE_SIZES: readonly ProductSize[] = [
  { label: '2–3 Y', inStock: true },
  { label: '4–5 Y', inStock: true },
  { label: '6–7 Y', inStock: true },
  { label: '8–9 Y', inStock: true },
  { label: '10–11 Y', inStock: false },
];

export const CATEGORY_NAMES: Readonly<Record<string, string>> = {
  'new-in': 'New in',
  girls: 'Girls',
  boys: 'Boys',
  sale: 'Sale',
};

export function titleCase(slug: string): string {
  return slug
    .split('-')
    .map((word) => word.charAt(0).toUpperCase() + word.slice(1))
    .join(' ');
}
