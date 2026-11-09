/** What a signed confirm link says about its order, read without changing anything (COD-12, COD-19). */
export interface CodLinkStatus {
  readonly isValid: boolean;
  readonly orderNumber: string | null;
  /** True only while the cash on delivery order is still waiting to be confirmed. */
  readonly canRespond: boolean;
  readonly statusCode: string | null;
}

export type CodAnswer = 'confirm' | 'cancel';

/** How a button press ended. The server never says more than this, so the page cannot be used to learn about other orders. */
export type CodOutcome = 'Confirmed' | 'Cancelled' | 'AlreadyHandled' | 'InvalidLink';

export type CodStatusResult = { readonly status: 'ok'; readonly link: CodLinkStatus } | { readonly status: 'error' };

export type CodRespondResult = { readonly status: 'ok'; readonly outcome: CodOutcome; readonly orderNumber: string | null } | { readonly status: 'error' };
