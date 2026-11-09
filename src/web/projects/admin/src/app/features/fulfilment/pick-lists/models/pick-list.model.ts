/** One order in the pick queue. The order number only: no customer data (PCK-11). */
export interface QueueItem {
  readonly orderId: number;
  readonly orderNumber: string;
  readonly status: string;
  readonly queuedAt: string;
  readonly ageMinutes: number;
  readonly lineCount: number;
  readonly unitCount: number;
  readonly claimedBy: string | null;
  readonly putBackRequired: boolean;
  readonly isCashOnDelivery: boolean;
}

/** One shelf stop on a pick list. A line waiting for restock has no bin. */
export interface PickRow {
  readonly lineId: number;
  readonly variantId: number;
  readonly binCode: string;
  readonly sku: string | null;
  readonly name: string;
  readonly colour: string;
  readonly size: string;
  readonly quantity: number;
  readonly picked: number;
  readonly status: string;
}

/** An order's pick list in walking order (PCK-1). */
export interface PickList {
  readonly orderId: number;
  readonly orderNumber: string;
  readonly status: string;
  readonly claimedBy: string | null;
  readonly currentBin: string | null;
  readonly nextBin: string | null;
  readonly readyToComplete: boolean;
  readonly putBackRequired: boolean;
  readonly rows: readonly PickRow[];
}

/** What a scan answered. A refusal is a normal answer, told in words (PCK-3, PCK-4). */
export interface ScanOutcome {
  readonly accepted: boolean;
  readonly code: string;
  readonly message: string;
  readonly nextBin: string | null;
  readonly currentBin: string | null;
  readonly readyToComplete: boolean;
  readonly pickedUnits: number;
  readonly totalUnits: number;
}

export interface CompleteResult {
  readonly completed: boolean;
  readonly alreadyDone: boolean;
}

/** Waiting to be picked; being picked. The pick queue shows both. */
export function isOpenForPicking(status: string): boolean {
  return status === 'QUEUED' || status === 'PICKING';
}

/** "12 min", "2 h 5 min": how long an order has waited (APP-8). */
export function ageText(minutes: number): string {
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
