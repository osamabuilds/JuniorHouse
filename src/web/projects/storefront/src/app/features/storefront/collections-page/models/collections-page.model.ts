import { ListingImage } from '../../listings';

/** One tile of GET /api/storefront/collections: a collection with live products, its piece count and the newest product's picture. */
export interface CollectionTile {
  readonly code: string;
  readonly name: string;
  readonly path: string;
  readonly pieces: number;
  readonly image: ListingImage | null;
}

export interface CollectionsPageData {
  readonly items: readonly CollectionTile[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly totalPages: number;
}

/** A failure is a result, not an exception, so the page can show it with the right status. */
export type CollectionsPageResult = { readonly status: 'ok'; readonly page: CollectionsPageData } | { readonly status: 'error' };
