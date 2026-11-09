/** The notes the server puts on a saved product (WSL-5, WSL-6). */
export type WishlistNote = 'PriceDropped' | 'OnSale' | 'SoldOut' | 'Unavailable';

/** One saved product as the wishlist page shows it. A product that is no longer published has no name, path or price. */
export interface WishlistItem {
  readonly productId: number;
  readonly name: string | null;
  readonly path: string | null;
  readonly price: number | null;
  readonly wasPrice: number | null;
  readonly savedPrice: number;
  readonly isAvailable: boolean;
  readonly imageUrl: string | null;
  readonly imageFallbackUrl: string | null;
  readonly imageWidth: number | null;
  readonly notes: readonly WishlistNote[];
  readonly savedAt: string;
}

export interface WishlistPage {
  readonly items: readonly WishlistItem[];
  readonly page: number;
  readonly pageSize: number;
  readonly totalCount: number;
  readonly totalPages: number;
}

export type ListResult = { readonly status: 'ok'; readonly page: WishlistPage } | { readonly status: 'signedOut' } | { readonly status: 'error' };

export type IdsResult = { readonly status: 'ok'; readonly ids: readonly number[] } | { readonly status: 'signedOut' } | { readonly status: 'error' };

export type ChangeResult =
  | { readonly status: 'ok' }
  | { readonly status: 'signedOut' }
  | { readonly status: 'notFound' }
  | { readonly status: 'refused'; readonly message: string }
  | { readonly status: 'error' };

/** The most guest products the server takes in one merge (WSL-3). */
export const MAX_GUEST_IDS = 50;

export const PAGE_SIZE = 24;
