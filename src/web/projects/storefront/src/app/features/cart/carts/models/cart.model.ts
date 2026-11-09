/** Why a basket line needs the shopper's attention. Shown in words, never by colour alone (CRT-5, CRT-7, CRT-8). */
export type LineIssue = 'unavailable' | 'low-stock' | 'price-changed';

/** One line as the server priced it. Every figure is the server's (SEC-09); the browser only displays them. */
export interface CartLine {
  readonly variantId: number;
  readonly productId: number;
  readonly name: string;
  readonly path: string;
  readonly colour: string;
  readonly size: string;
  readonly sku: string | null;
  readonly unitPrice: number | null;
  readonly compareAtPrice: number | null;
  readonly quantity: number;
  readonly lineTotal: number;
  readonly available: number;
  readonly previousPrice: number | null;
  readonly issues: readonly LineIssue[];
  /** The resized product picture (WebP) and its JPEG fallback, site-relative; null when the product has no finished picture yet. */
  readonly imageUrl: string | null;
  readonly imageFallbackUrl: string | null;
  readonly imageAlt: string | null;
}

export interface Cart {
  readonly lines: readonly CartLine[];
  readonly itemCount: number;
  readonly subtotal: number;
  /** True when a line is unavailable or short of stock: checkout must wait until the shopper fixes the basket. */
  readonly hasProblems: boolean;
  /** Money off from the applied code, the server's figure; 0 when none. */
  readonly discount: number;
  /** Delivery once a city is known; null while it is still "calculated at checkout". */
  readonly delivery: number | null;
  /** What the shopper pays, excluding delivery while it is unknown. */
  readonly total: number;
  readonly discountCode: string | null;
  /** Things the server did that the shopper should know (a code taken off because the bag changed, DSC-13). */
  readonly notices: readonly string[];
}

export const EMPTY_CART: Cart = { lines: [], itemCount: 0, subtotal: 0, hasProblems: false, discount: 0, delivery: null, total: 0, discountCode: null, notices: [] };

/** The most of one item in an order; the server enforces it, this only stops the stepper offering more (CRT-11). */
export const MAX_PER_LINE = 10;

/** What a basket call turns into. Nothing throws into the store or the page. */
export type CartResult =
  | { readonly status: 'ok'; readonly cart: Cart }
  | { readonly status: 'refused'; readonly message: string }
  | { readonly status: 'error' };

/** 'quick-add' is an add from a product card: the bag count changes but the drawer does not open (the card confirms in place). */
export type CartChange = 'add' | 'quick-add' | 'set' | 'remove';
