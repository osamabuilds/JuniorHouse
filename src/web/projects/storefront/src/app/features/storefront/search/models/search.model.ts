import { ListingImage, ListingOptions, ListingTile, Suggestion } from '../../listings';

/** The shapes GET /api/storefront/search, /search/suggest and /search/sku return. */

/** One line of autosuggest: a product (with picture and price) or a category page. */
export interface SuggestionItem {
  readonly kind: 'product' | 'category';
  readonly label: string;
  readonly path: string;
  readonly image: ListingImage | null;
  readonly price: number | null;
}

export interface SuggestResult {
  /** The normalised text the server answered for. */
  readonly query: string;
  readonly items: readonly SuggestionItem[];
}

/** Where an exact SKU or style code leads: a product, with the colour and size of the SKU selected. */
export interface SkuTarget {
  readonly path: string;
  readonly colour: string | null;
  readonly size: string | null;
}

/** A page of results. With no match `total` is 0 and `tiles` is empty. */
export interface SearchPageData {
  readonly query: string;
  readonly tiles: readonly ListingTile[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly totalPages: number;
  readonly maxSavingPercent: number;
  readonly options: ListingOptions;
}

/** What the results page shows once a request has finished. */
export type SearchResult =
  | { readonly status: 'ok'; readonly page: SearchPageData; readonly suggestions: readonly Suggestion[] }
  | { readonly status: 'redirect'; readonly url: string }
  | { readonly status: 'notFound' }
  | { readonly status: 'error' };
