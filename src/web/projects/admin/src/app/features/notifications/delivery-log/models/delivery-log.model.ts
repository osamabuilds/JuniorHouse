/** One message on one channel, as the staff delivery log lists it (docs/specs/notifications/delivery-log, DLG-1). Recipients arrive masked; no body is ever sent (DLG-6). */
export interface DeliveryLogRow {
  readonly id: number;
  readonly notificationId: number;
  readonly kind: string;
  readonly channel: string;
  readonly status: string;
  readonly attempts: number;
  readonly lastError: string | null;
  readonly orderId: number | null;
  readonly orderNumber: string | null;
  readonly recipient: string | null;
  readonly createdAt: string;
  readonly nextAttemptAt: string | null;
  readonly isResend: boolean;
  readonly resentBy: string | null;
  readonly canResend: boolean;
}

export interface DeliveryAttempt {
  readonly attemptNumber: number;
  readonly outcome: string;
  readonly error: string | null;
  readonly at: string;
}

export interface DeliveryDetail {
  readonly row: DeliveryLogRow;
  readonly expiresAt: string;
  readonly resendOfId: number | null;
  readonly attempts: readonly DeliveryAttempt[];
  readonly deadAt: string | null;
  readonly resentBy: string | null;
  readonly resentAt: string | null;
  /** A second copy would reach someone who probably has the first (DLG-7). */
  readonly warnsOfDuplicate: boolean;
}

/** The strip above the log: queue age, what needs a person, today's WhatsApp sends (DLV-11, CHN-15). */
export interface DeliverySummary {
  readonly countsByStatus: Readonly<Record<string, number>>;
  readonly queuedCount: number;
  readonly deadLettersAwaitingResend: number;
  readonly oldestDueSeconds: number;
  readonly whatsAppSentToday: number;
}

/** What the list can narrow by. Empty means "any". Codes are the server's four-letter lookup codes. */
export interface DeliveryFilters {
  readonly status: string;
  readonly channel: string;
  readonly kind: string;
  readonly deadLettersOnly: boolean;
  readonly orderId: number | null;
  readonly search: string;
}

export const NO_FILTERS: DeliveryFilters = { status: '', channel: '', kind: '', deadLettersOnly: false, orderId: null, search: '' };

export interface Choice {
  readonly code: string;
  readonly name: string;
}

export const STATUS_CHOICES: readonly Choice[] = [
  { code: 'QUED', name: 'Queued' },
  { code: 'SENT', name: 'Sent' },
  { code: 'DLVD', name: 'Delivered' },
  { code: 'FAIL', name: 'Failed' },
  { code: 'DEAD', name: 'Gave up (dead letter)' },
  { code: 'EXPR', name: 'Expired' },
  { code: 'SKIP', name: 'Skipped' },
];

export const CHANNEL_CHOICES: readonly Choice[] = [
  { code: 'EMAL', name: 'Email' },
  { code: 'WHTS', name: 'WhatsApp' },
];

export const KIND_CHOICES: readonly Choice[] = [
  { code: 'ORCF', name: 'Order confirmation' },
  { code: 'ORCN', name: 'Order cancelled' },
  { code: 'CODR', name: 'COD confirmation request' },
];

export const ERROR_CHOICES: readonly Choice[] = [
  { code: 'TMPY', name: 'Temporary problem' },
  { code: 'PERM', name: 'Refused by the provider' },
  { code: 'CIRC', name: 'Channel paused after repeated failures' },
  { code: 'OPTO', name: 'Not opted in, or the customer said stop' },
  { code: 'NOAD', name: 'No address or number to send to' },
];

/** The answer to a resend: the id of the new delivery, queued or found again by its key. */
export interface ResendResult {
  readonly id: number;
}
