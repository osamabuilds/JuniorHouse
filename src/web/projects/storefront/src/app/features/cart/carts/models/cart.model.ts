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
  readonly imageKey: string | null;
  readonly imageAlt: string | null;
  readonly imageWidth: number | null;
  readonly imageHeight: number | null;
}

export interface Cart {
  readonly lines: readonly CartLine[];
  readonly itemCount: number;
  readonly subtotal: number;
  /** True when a line is unavailable or short of stock: checkout must wait until the shopper fixes the basket. */
  readonly hasProblems: boolean;
}

export const EMPTY_CART: Cart = { lines: [], itemCount: 0, subtotal: 0, hasProblems: false };

/** The most of one item in an order; the server enforces it, this only stops the stepper offering more (CRT-11). */
export const MAX_PER_LINE = 10;

/** What a basket call turns into. Nothing throws into the store or the page. */
export type CartResult =
  | { readonly status: 'ok'; readonly cart: Cart }
  | { readonly status: 'refused'; readonly message: string }
  | { readonly status: 'error' };

export type CartChange = 'add' | 'set' | 'remove';
