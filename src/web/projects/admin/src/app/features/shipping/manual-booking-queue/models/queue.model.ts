/** One parcel waiting for a booking (MBQ-1): why it waits, how often it was tried, and how long it has waited. */
export interface QueueRow {
  readonly id: number;
  readonly orderId: number;
  readonly orderNumber: string;
  readonly reason: string | null;
  readonly note: string | null;
  readonly attempts: number;
  readonly needsStaff: boolean;
  readonly waitingMinutes: number;
  readonly nextAttemptAt: string | null;
  readonly cashToCollect: number;
}

export interface CourierChoice {
  readonly code: string;
  readonly name: string;
}

export const COURIER_CHOICES: readonly CourierChoice[] = [
  { code: 'PRIMARY', name: 'Primary courier' },
  { code: 'SECONDARY', name: 'Secondary courier' },
];

/** The reason a parcel waits, in words (MBQ-1, MBQ-8). */
export function reasonWords(row: QueueRow): string {
  if (row.reason === 'REJECTED') {
    return `A courier refused the parcel${row.note ? `: ${row.note}` : ''}. It will not be retried by itself.`;
  }

  if (row.needsStaff) {
    return 'The automatic tries are used up. It needs you.';
  }

  return row.reason === 'UNREACHABLE' ? 'No courier could be reached. It will be tried again.' : 'Waiting to be tried.';
}

/** "12 min", "2 h 5 min": how long the parcel has been waiting. */
export function waitingText(minutes: number): string {
  if (minutes < 1) {
    return 'just now';
  }

  if (minutes < 60) {
    return `${minutes} min`;
  }

  const hours = Math.floor(minutes / 60);
  const rest = minutes % 60;
  return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}
