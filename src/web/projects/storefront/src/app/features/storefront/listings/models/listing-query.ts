/**
 * The listing scope and filter as the page sees them, parsed and clamped from the URL the same way the API does (LST-11, 16), so a hostile
 * query string never reaches the page, and one filter always has one spelling (SEO-06). Pure.
 */

export type ListingScopeKind = 'audience' | 'category' | 'new-in' | 'sale' | 'collection';

export interface ListingScope {
  readonly kind: ListingScopeKind;
  readonly gender?: string;
  readonly category?: string;
  readonly collection?: string;
}

export type ListingSort = 'recent' | 'price-asc' | 'price-desc';

export interface ListingFilter {
  readonly sizes: readonly string[];
  readonly colours: readonly string[];
  readonly age: string | null;
  readonly priceMin: number | null;
  readonly priceMax: number | null;
  readonly sort: ListingSort;
  readonly page: number;
}

export const EMPTY_FILTER: ListingFilter = { sizes: [], colours: [], age: null, priceMin: null, priceMax: null, sort: 'recent', page: 1 };

const MAX_VALUES = 20;
const MAX_PRICE = 1_000_000;
const CODE = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const NUMBER = /^[0-9]+(\.[0-9]{1,2})?$/;

type RawQuery = Readonly<Record<string, string | readonly string[] | null | undefined>>;

function all(raw: RawQuery, key: string): readonly string[] {
  const v = raw[key];
  return v == null ? [] : typeof v === 'string' ? [v] : v;
}

function codes(values: readonly string[]): string[] {
  const seen = new Set<string>();
  for (const v of values) {
    const c = v.trim().toLowerCase();
    if (c.length > 0 && c.length <= 40 && CODE.test(c)) seen.add(c);
    if (seen.size >= MAX_VALUES) break;
  }
  return [...seen];
}

function price(text: string): number | null | undefined {
  if (text === '') return null;
  if (!NUMBER.test(text)) return undefined;
  const n = Number(text);
  return n <= MAX_PRICE ? n : undefined;
}

/** Reads the filter from a query. Anything malformed is dropped (the page then redirects to the clean spelling). */
export function parseFilter(raw: RawQuery): ListingFilter {
  let priceMin: number | null = null;
  let priceMax: number | null = null;
  const parts = (all(raw, 'price')[0] ?? '').split('-');
  if (parts.length === 2) {
    const lo = price(parts[0]);
    const hi = price(parts[1]);
    if (lo !== undefined && hi !== undefined && (lo === null || hi === null || lo <= hi)) {
      priceMin = lo;
      priceMax = hi;
    }
  }
  const sort = all(raw, 'sort')[0];
  const pageText = all(raw, 'page')[0] ?? '1';
  const page = /^[0-9]{1,9}$/.test(pageText) && Number(pageText) >= 1 ? Number(pageText) : 1;
  return {
    sizes: codes(all(raw, 'size')),
    colours: codes(all(raw, 'colour')),
    age: codes(all(raw, 'age'))[0] ?? null,
    priceMin,
    priceMax,
    sort: sort === 'price-asc' || sort === 'price-desc' ? sort : 'recent',
    page,
  };
}

/** The canonical query: fixed key order, sorted values, defaults left out. */
export function toCanonicalQuery(filter: ListingFilter, includePage = true): Record<string, string | string[]> {
  const q: Record<string, string | string[]> = {};
  if (filter.age) q['age'] = filter.age;
  if (filter.sizes.length > 0) q['size'] = [...filter.sizes].sort();
  if (filter.colours.length > 0) q['colour'] = [...filter.colours].sort();
  if (filter.priceMin !== null || filter.priceMax !== null) q['price'] = `${filter.priceMin ?? ''}-${filter.priceMax ?? ''}`;
  if (filter.sort !== 'recent') q['sort'] = filter.sort;
  if (includePage && filter.page > 1) q['page'] = String(filter.page);
  return q;
}

/** The canonical query as a string, for comparing with what the visitor asked for. */
export function toQueryString(filter: ListingFilter, includePage = true): string {
  const q = toCanonicalQuery(filter, includePage);
  return Object.entries(q)
    .flatMap(([k, v]) => (typeof v === 'string' ? [`${k}=${v}`] : v.map((x) => `${k}=${x}`)))
    .join('&');
}

/** Parameters for the API (the scope as query values plus the canonical filter). */
export function toApiQuery(scope: ListingScope, filter: ListingFilter): Record<string, string | string[]> {
  const q: Record<string, string | string[]> = { scope: scope.kind, ...toCanonicalQuery(filter) };
  if (scope.gender) q['gender'] = scope.gender;
  if (scope.category) q['category'] = scope.category;
  if (scope.collection) q['collection'] = scope.collection;
  return q;
}

/** A stable key for a scope and filter, to tell a finished request from a stale one. */
export function listingKey(scope: ListingScope, filter: ListingFilter): string {
  return `${scope.kind}|${scope.gender ?? ''}|${scope.category ?? ''}|${scope.collection ?? ''}|${toQueryString(filter)}`;
}

/** The path of a scope: /girls, /girls/dresses, /girls/new-in, /new-in, /collections/summer. */
export function scopePath(scope: ListingScope): string {
  switch (scope.kind) {
    case 'audience':
      return `/${scope.gender}`;
    case 'category':
      return `/${scope.gender}/${scope.category}`;
    case 'new-in':
    case 'sale':
      return scope.gender ? `/${scope.gender}/${scope.kind}` : `/${scope.kind}`;
    case 'collection':
      return `/collections/${scope.collection}`;
  }
}
