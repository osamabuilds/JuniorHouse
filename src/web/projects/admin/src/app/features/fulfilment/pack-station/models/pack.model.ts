export interface PackLine {
  readonly lineId: number;
  readonly sku: string | null;
  readonly name: string;
  readonly colour: string;
  readonly size: string;
  readonly quantity: number;
  readonly packed: number;
}

/**
 * An order at the pack station: what to scan, the packaging to use (PAK-4), the weight to expect, the weight recorded and the cash to collect shown
 * large for cash on delivery (PAK-11). The order number and amounts only: no customer data.
 */
export interface PackView {
  readonly orderId: number;
  readonly orderNumber: string;
  readonly status: string;
  readonly claimedBy: string | null;
  readonly isCashOnDelivery: boolean;
  readonly cashToCollect: number;
  readonly packaging: string;
  readonly expectedGrams: number;
  readonly lines: readonly PackLine[];
  readonly allScanned: boolean;
  readonly weightGrams: number | null;
  readonly weightDeviates: boolean;
  readonly note: string | null;
}

export interface PackScanOutcome {
  readonly accepted: boolean;
  readonly code: string;
  readonly message: string;
  readonly allScanned: boolean;
  readonly packedUnits: number;
  readonly totalUnits: number;
}

export interface WeightOutcome {
  readonly weightGrams: number;
  readonly expectedGrams: number;
  readonly allowedGrams: number;
  readonly deviationGrams: number;
  readonly withinTolerance: boolean;
  readonly packaging: string;
  readonly message: string;
}

export interface PackComplete {
  readonly packed: boolean;
  readonly alreadyDone: boolean;
}

/** The words for the packaging the rule recommends (PAK-4). */
export function packagingName(code: string): string {
  return code === 'POLY_BAG' ? 'Poly bag' : code === 'BOX' ? 'Box' : code;
}

/** The heaviest parcel the scale can honestly report (PAK-8); the server refuses more. */
export const MAX_PARCEL_GRAMS = 30000;
