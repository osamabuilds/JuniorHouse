/** One row of the staff order list (docs/specs/orders/order-queue, OQ-1). */
export interface OrderListItem {
  readonly id: number;
  readonly orderNumber: string;
  readonly statusId: number;
  readonly statusName: string;
  readonly methodName: string;
  readonly total: number;
  readonly placedAt: string;
  readonly contactName: string;
  readonly cityName: string;
  readonly itemCount: number;
  readonly codNextDueAt: string | null;
  readonly codAutoCancelAt: string | null;
  readonly isFirstOrder: boolean;
  readonly isHighValue: boolean;
  readonly isRefusalFlagged: boolean;
}

export interface OrderLine {
  readonly variantId: number;
  readonly name: string;
  readonly sku: string | null;
  readonly colour: string;
  readonly size: string;
  readonly unitPrice: number;
  readonly quantity: number;
  readonly lineTotal: number;
}

export interface OrderHistoryEntry {
  readonly fromStatus: string;
  readonly toStatus: string;
  readonly reason: string | null;
  readonly actor: string;
  readonly actorRef: string | null;
  readonly note: string | null;
  readonly at: string;
}

export interface CodAttempt {
  readonly number: number;
  readonly outcome: string;
  readonly actor: string;
  readonly actorRef: string | null;
  readonly note: string | null;
  readonly at: string;
}

/** One order as staff see it (OQ-7). The phone and address are for staff who must call and pack. */
export interface OrderDetail {
  readonly id: number;
  readonly orderNumber: string;
  readonly statusId: number;
  readonly statusName: string;
  readonly methodName: string;
  readonly isCashOnDelivery: boolean;
  readonly placedAt: string;
  readonly contactName: string;
  readonly contactEmail: string | null;
  readonly contactMobile: string;
  readonly whatsAppOptIn: boolean;
  readonly recipientName: string;
  readonly recipientPhone: string;
  readonly addressLine: string;
  readonly cityName: string;
  readonly provinceName: string;
  readonly postalCode: string | null;
  readonly deliveryNote: string | null;
  readonly subtotal: number;
  readonly discount: number;
  readonly shipping: number;
  readonly total: number;
  readonly discountCode: string | null;
  readonly isFirstOrder: boolean;
  readonly isHighValue: boolean;
  readonly isRefusalFlagged: boolean;
  readonly codNextDueAt: string | null;
  readonly codAutoCancelAt: string | null;
  readonly codAttemptCount: number;
  readonly canRecordCodOutcome: boolean;
  readonly canCancel: boolean;
  readonly lines: readonly OrderLine[];
  readonly history: readonly OrderHistoryEntry[];
  readonly codAttempts: readonly CodAttempt[];
}

export type CodOutcomeChoice = 'confirmed' | 'no-answer' | 'refused';

/** The filters of the list. Empty means "any". */
export interface OrderFilters {
  readonly status: number | null;
  readonly method: number | null;
  readonly search: string;
  readonly callDueNow: boolean;
  readonly flagged: boolean;
}

export const NO_FILTERS: OrderFilters = { status: null, method: null, search: '', callDueNow: false, flagged: false };

export const STATUS_CHOICES: readonly { readonly id: number; readonly name: string }[] = [
  { id: 1, name: 'Pending confirmation' },
  { id: 2, name: 'Confirmed' },
  { id: 3, name: 'Payment pending' },
  { id: 4, name: 'Paid' },
  { id: 5, name: 'Cancelled' },
  { id: 6, name: 'Dispatched' },
  { id: 7, name: 'Delivered' },
];

export const METHOD_CHOICES: readonly { readonly id: number; readonly name: string }[] = [
  { id: 1, name: 'Cash on Delivery' },
  { id: 2, name: 'Easypaisa' },
  { id: 3, name: 'JazzCash' },
  { id: 4, name: 'Card' },
];
