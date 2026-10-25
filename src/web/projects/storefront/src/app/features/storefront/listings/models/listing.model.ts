/** The shapes GET /api/storefront/listings, /options and /empty return. */

export interface ListingImageSource {
  readonly width: number;
  readonly webp: string;
  readonly jpeg: string | null;
}

export interface ListingImage {
  readonly altText: string;
  readonly width: number;
  readonly height: number;
  readonly sources: readonly ListingImageSource[];
}

/** A size the product comes in; sold out when no colour of it can be bought. */
export interface ListingTileSize {
  readonly code: string;
  readonly name: string;
  readonly soldOut: boolean;
}

export interface ListingTile {
  readonly path: string;
  readonly name: string;
  readonly price: number | null;
  readonly compareAtPrice: number | null;
  readonly savingPercent: number;
  /** The server difference in PKR between the compare-at price and the price; 0 when there is no discount (SEC-09). */
  readonly savingAmount: number;
  readonly colourCount: number;
  readonly hasSoldOutSizes: boolean;
  readonly sizes: readonly ListingTileSize[];
  readonly image: ListingImage | null;
}

export interface Breadcrumb {
  readonly label: string;
  readonly path: string | null;
}

export type ScopeKind = 'Audience' | 'Category' | 'NewIn' | 'Sale' | 'Collection' | 'AllNewIn' | 'AllSale' | 'All';

export interface ListingPage {
  readonly title: string;
  readonly scopeKind: ScopeKind;
  readonly audienceName: string | null;
  readonly categoryName: string | null;
  readonly breadcrumbs: readonly Breadcrumb[];
  readonly tiles: readonly ListingTile[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly totalPages: number;
  readonly maxSavingPercent: number;
}

export interface ListingOption {
  readonly value: string;
  readonly label: string;
}

export interface ListingOptions {
  readonly sizes: readonly ListingOption[];
  readonly colours: readonly ListingOption[];
  readonly priceMin: number | null;
  readonly priceMax: number | null;
  readonly ages: readonly ListingOption[];
}

export interface Suggestion {
  readonly label: string;
  readonly path: string;
}

export interface EmptyState {
  readonly suggestions: readonly Suggestion[];
}

/** What the page shows once a request has finished. */
export type ListingResult =
  | { readonly status: 'ok'; readonly page: ListingPage; readonly options: ListingOptions; readonly empty: EmptyState | null }
  | { readonly status: 'notFound' }
  | { readonly status: 'error' };
