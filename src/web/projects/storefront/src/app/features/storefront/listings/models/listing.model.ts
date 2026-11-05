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
  /** The variant a quick add would put in the bag: the colour the card shows, or another colour when that one lacks the size. */
  readonly variantId?: number;
  /** InStock, LowStock, SoldOut or NotYetAvailable for that variant. A hint: the server decides when the bag is changed. */
  readonly availability?: string;
  /** The name of that variant's colour, so the confirmation can say which colour was added. */
  readonly colourName?: string;
}

export interface ListingTile {
  /** The Catalog product id, for the wishlist heart. */
  readonly productId: number;
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
  /** Published within the New In window. */
  readonly isNew?: boolean;
  /** Some size is low on stock (the product page's rule). */
  readonly hasLowStock?: boolean;
  /** The next picture of the product, for the hover and touch views. */
  readonly secondImage?: ListingImage | null;
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
