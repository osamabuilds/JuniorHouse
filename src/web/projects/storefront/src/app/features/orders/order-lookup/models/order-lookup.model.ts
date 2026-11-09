/** One line of a tracked order. */
export interface TrackedLine {
  readonly name: string;
  readonly colour: string;
  readonly size: string;
  readonly quantity: number;
  readonly lineTotal: number;
}

/**
 * What a guest may see of an order (OLK-1, OLK-6): the status, the items and the totals. The server masks the mobile and sends only the
 * city, so there is no street, recipient or full phone number here to show.
 */
export interface TrackedOrder {
  readonly orderNumber: string;
  readonly statusCode: string;
  readonly statusName: string;
  readonly placedAt: string;
  readonly paymentMethodCode: string;
  readonly maskedMobile: string;
  readonly city: string;
  readonly subtotal: number;
  readonly discount: number;
  readonly shipping: number;
  readonly total: number;
  readonly lines: readonly TrackedLine[];
}

export interface LookupInput {
  readonly orderNumber: string;
  readonly mobile: string;
  readonly captchaToken: string | null;
}

/**
 * How a look-up ended. A wrong number, a wrong mobile and both are the same `mismatch` (SEC-07): the page never learns which was wrong, so it
 * cannot say.
 */
export type LookupResult =
  | { readonly kind: 'found'; readonly order: TrackedOrder }
  | { readonly kind: 'mismatch' }
  | { readonly kind: 'captcha' }
  | { readonly kind: 'limited' }
  | { readonly kind: 'error' };
