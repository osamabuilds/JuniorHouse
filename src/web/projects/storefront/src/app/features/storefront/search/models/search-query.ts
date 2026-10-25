/**
 * What a visitor typed, cleaned the same way the API does (search SRC-3): control and invisible characters out, whitespace collapsed,
 * cut at 100 characters. The result is plain text; templates encode it when they show it. Pure.
 */
import { ListingFilter, toQueryString } from '../../listings';
import { SkuTarget } from './search.model';

export const MAX_QUERY_LENGTH = 100;

const INVISIBLE = /[\p{Cc}\p{Cf}\p{Cs}\p{Co}]/gu;
const CODE_SHAPE = /^[a-z0-9]+(-[a-z0-9]+)*$/;

export function normaliseQuery(raw: string | null | undefined): string {
  if (!raw) return '';
  // Bound the work first, as the server does.
  const bounded = raw.length > MAX_QUERY_LENGTH * 4 ? raw.slice(0, MAX_QUERY_LENGTH * 4) : raw;
  const text = bounded.normalize('NFKC').replace(INVISIBLE, ' ').replace(/\s+/g, ' ').trim().toLowerCase();
  if (text.length <= MAX_QUERY_LENGTH) return text;
  // Never leave half a surrogate pair at the cut.
  const cut = text.slice(0, MAX_QUERY_LENGTH);
  return (/[\uD800-\uDBFF]$/.test(cut) ? cut.slice(0, -1) : cut).trim();
}

/** The text in the lower-case form a SKU is stored in, or null when it cannot be a SKU or style code (one word of letters, digits, hyphens). */
export function skuCandidate(query: string): string | null {
  return query.length >= 2 && query.length <= 120 && CODE_SHAPE.test(query) ? query : null;
}

/** The product address for a SKU: its colour and size become the page's selection (Q-SRC-3). */
export function skuTargetUrl(target: SkuTarget): string {
  const parts: string[] = [];
  if (target.colour) parts.push(`colour=${encodeURIComponent(target.colour)}`);
  if (target.size) parts.push(`size=${encodeURIComponent(target.size)}`);
  return parts.length > 0 ? `${target.path}?${parts.join('&')}` : target.path;
}

/** A stable key for a request, to tell a finished one from a stale one. */
export function searchKey(query: string, filter: ListingFilter): string {
  return `${query}|${toQueryString(filter)}`;
}

/** The canonical address of a results page: the text and the filter, in one fixed order. */
export function searchUrl(query: string, filter: ListingFilter): string {
  const rest = toQueryString(filter);
  return `/search?q=${encodeURIComponent(query)}${rest ? `&${rest}` : ''}`;
}
