export type ManifestStatus = 'OPEN' | 'MATCHED' | 'MISMATCH' | 'CLOSED';

/** One manifest in the list (MNF-1, MNF-10). */
export interface ManifestSummary {
  readonly id: number;
  readonly courierName: string;
  readonly businessDate: string;
  readonly status: ManifestStatus;
  readonly parcelCount: number;
  readonly countedParcels: number | null;
  readonly countedBy: string | null;
  readonly countedAt: string | null;
}

export interface ManifestLine {
  readonly shipmentId: number;
  readonly orderNumber: string;
  readonly waybill: string;
  readonly cashToCollect: number;
  readonly weightGrams: number;
  readonly handedOver: boolean;
}

export interface ManifestDetail {
  readonly summary: ManifestSummary;
  readonly note: string | null;
  readonly totalCash: number;
  readonly totalWeightGrams: number;
  readonly lines: readonly ManifestLine[];
}

/** The status in words; colour is never the only cue (MNF-5). */
export function statusWords(summary: ManifestSummary): string {
  switch (summary.status) {
    case 'MATCHED':
      return 'Matched: the count agrees';
    case 'MISMATCH':
      return `Mismatch: ${differenceText(summary)}`;
    case 'CLOSED':
      return 'Closed';
    default:
      return 'Open';
  }
}

/** "2 parcels short" / "1 parcel more than listed" (MNF-5). */
export function differenceText(summary: ManifestSummary): string {
  if (summary.countedParcels === null) {
    return 'not counted yet';
  }

  const diff = summary.countedParcels - summary.parcelCount;
  const n = Math.abs(diff);
  const unit = n === 1 ? 'parcel' : 'parcels';
  return diff === 0 ? 'no difference' : diff < 0 ? `${n} ${unit} short` : `${n} ${unit} more than listed`;
}
