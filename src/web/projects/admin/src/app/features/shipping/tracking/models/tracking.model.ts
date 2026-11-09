/** One status the courier reported (TRK-13): the status, its words, the courier's event id, when it happened and where it came from (a push or a poll). */
export interface TrackingEvent {
  readonly status: string;
  readonly statusName: string;
  readonly courierEventId: string;
  readonly occurredAt: string;
  readonly source: string;
}

/** A shipment's status as the staff screens name it. A failed attempt and a pending booking get attention. */
export function needsAttention(status: string): boolean {
  return status === 'FAILED_ATTEMPT' || status === 'PENDING_BOOKING' || status === 'RETURNED_TO_ORIGIN';
}

export function sourceName(source: string): string {
  return source === 'PUSH' ? 'Courier pushed it' : source === 'POLL' ? 'We asked the courier' : 'Raised by Romp';
}
