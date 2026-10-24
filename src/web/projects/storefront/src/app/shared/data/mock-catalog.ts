/** Placeholder catalogue for the home, listing and product pages until the published-catalog read model exists (replace with the API). */
export type Tone = 'oat' | 'stone' | 'clay' | 'sage' | 'sand' | 'dusk';
export type GarmentShape = 'dress' | 'shirt' | 'trouser' | 'hat';

export interface CatalogProduct {
  readonly slug: string;
  readonly name: string;
  readonly audience: 'girls' | 'boys';
  readonly type: string;
  readonly ages: string;
  readonly price: number;
  readonly tone: Tone;
  readonly altTone: Tone;
  readonly shape: GarmentShape;
  readonly tag?: 'New' | 'Low stock';
  /** Youngest and oldest age the piece is made in; the age filter matches on overlap. */
  readonly minAge: number;
  readonly maxAge: number;
}

export const CATALOG: readonly CatalogProduct[] = [
  { slug: 'linen-shirt-dress', name: 'Linen shirt dress', audience: 'girls', type: 'dresses', ages: '2–10Y', price: 4990, tone: 'oat', altTone: 'stone', shape: 'dress', minAge: 2, maxAge: 10 },
  { slug: 'boxy-poplin-shirt', name: 'Boxy poplin shirt', audience: 'boys', type: 'shirts', ages: '2–12Y', price: 3290, tone: 'sage', altTone: 'oat', shape: 'shirt', tag: 'New', minAge: 2, maxAge: 12 },
  { slug: 'wide-leg-trouser', name: 'Wide-leg trouser', audience: 'girls', type: 'trousers', ages: '2–12Y', price: 3990, tone: 'stone', altTone: 'sand', shape: 'trouser', minAge: 2, maxAge: 12 },
  { slug: 'smocked-sundress', name: 'Smocked sundress', audience: 'girls', type: 'dresses', ages: '1–8Y', price: 4290, tone: 'clay', altTone: 'oat', shape: 'dress', tag: 'Low stock', minAge: 1, maxAge: 8 },
  { slug: 'cable-knit-cardigan', name: 'Cable-knit cardigan', audience: 'girls', type: 'knitwear', ages: '1–12Y', price: 5490, tone: 'sand', altTone: 'sage', shape: 'shirt', tag: 'New', minAge: 1, maxAge: 12 },
  { slug: 'pleated-chino', name: 'Pleated chino', audience: 'boys', type: 'trousers', ages: '2–12Y', price: 3790, tone: 'sage', altTone: 'stone', shape: 'trouser', minAge: 2, maxAge: 12 },
  { slug: 'canvas-bucket-hat', name: 'Canvas bucket hat', audience: 'boys', type: 'accessories', ages: 'One size', price: 1490, tone: 'stone', altTone: 'clay', shape: 'hat', minAge: 1, maxAge: 12 },
  { slug: 'pinafore-dress', name: 'Pinafore dress', audience: 'girls', type: 'dresses', ages: '2–8Y', price: 3890, tone: 'sand', altTone: 'clay', shape: 'dress', minAge: 2, maxAge: 8 },
  { slug: 'cotton-rib-tee', name: 'Cotton rib tee', audience: 'boys', type: 'shirts', ages: '1–12Y', price: 1790, tone: 'oat', altTone: 'clay', shape: 'shirt', minAge: 1, maxAge: 12 },
  { slug: 'linen-shorts', name: 'Linen shorts', audience: 'boys', type: 'trousers', ages: '2–10Y', price: 2490, tone: 'clay', altTone: 'sand', shape: 'trouser', minAge: 2, maxAge: 10 },
  { slug: 'tiered-midi-dress', name: 'Tiered midi dress', audience: 'girls', type: 'dresses', ages: '4–12Y', price: 5290, tone: 'sage', altTone: 'oat', shape: 'dress', minAge: 4, maxAge: 12 },
  { slug: 'quilted-jacket', name: 'Quilted jacket', audience: 'boys', type: 'outerwear', ages: '2–12Y', price: 6490, tone: 'stone', altTone: 'sage', shape: 'shirt', minAge: 2, maxAge: 12 },
];

export interface Category {
  readonly slug: string;
  readonly name: string;
  readonly lead: string;
}

export const CATEGORIES: readonly Category[] = [
  { slug: 'new-in', name: 'New in', lead: 'The latest pieces, for ages 1 to 12.' },
  { slug: 'girls', name: 'Girls', lead: 'Dresses, knitwear and easy separates for ages 1 to 12.' },
  { slug: 'boys', name: 'Boys', lead: 'Shirts, trousers and layers built for play, ages 1 to 12.' },
  { slug: 'sale', name: 'Sale', lead: 'Selected pieces at a lower price, while sizes last.' },
];

export const CATEGORY_NAMES: Readonly<Record<string, string>> = Object.fromEntries(CATEGORIES.map((c) => [c.slug, c.name]));

export const AGE_BANDS = [
  { value: '1-3', label: '1–3Y', min: 1, max: 3 },
  { value: '4-8', label: '4–8Y', min: 4, max: 8 },
  { value: '9-12', label: '9–12Y', min: 9, max: 12 },
] as const;

export const SIZES: readonly { label: string; inStock: boolean }[] = [
  { label: '1–2Y', inStock: true },
  { label: '2–3Y', inStock: true },
  { label: '3–4Y', inStock: true },
  { label: '4–5Y', inStock: true },
  { label: '6–7Y', inStock: true },
  { label: '8–9Y', inStock: false },
  { label: '10–11Y', inStock: true },
];

export function productUrl(p: CatalogProduct): string {
  return `/${p.audience}/${p.type}/${p.slug}`;
}

export function productsFor(category: string): readonly CatalogProduct[] {
  if (category === 'girls' || category === 'boys') return CATALOG.filter((p) => p.audience === category);
  if (category === 'new-in') return CATALOG.filter((p) => p.tag === 'New' || p.minAge <= 2).slice(0, 8);
  if (category === 'sale') return CATALOG.filter((p) => p.price <= 3500);
  return CATALOG;
}

export function titleCase(slug: string): string {
  return slug
    .split('-')
    .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
    .join(' ');
}
