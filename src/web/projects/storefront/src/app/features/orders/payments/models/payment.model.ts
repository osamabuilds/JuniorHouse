/** What the return page can say about a payment. Only the stored state decides: the browser coming back proves nothing (PAY-10). */
export type PaymentState = 'confirming' | 'paid' | 'failed' | 'expired' | 'cancelled' | 'review' | 'cod';

export interface PaymentStatus {
  readonly state: PaymentState;
  readonly orderNumber: string;
  readonly total: number;
  /** True while the shopper may try the payment again (a decline, with tries and time left). */
  readonly canRetry: boolean;
  readonly methodCode: string;
}

export type StatusResult = { readonly status: 'ok'; readonly payment: PaymentStatus } | { readonly status: 'invalid' } | { readonly status: 'error' };

/** The ways starting a payment ends. The server says only this much, so a page cannot be used to learn about other orders. */
export type StartKind =
  | 'Started'
  | 'Reused'
  | 'AlreadyPaid'
  | 'OrderClosed'
  | 'WindowClosed'
  | 'TooManyAttempts'
  | 'GatewayUnavailable'
  | 'UnderReview';

export type StartResult =
  | { readonly status: 'ok'; readonly kind: StartKind; readonly redirectUrl: string | null }
  | { readonly status: 'invalid' }
  | { readonly status: 'error' };
