/**
 * One row of the Notify Me demand list (NTM-11): how many people wait for one size of one product. Counts only: the API never sends an
 * address or any other personal data, and this type has no field for one (NTM-12).
 */
export interface NotifyDemandRow {
  readonly variantId: number;
  readonly productName: string;
  readonly colourName: string;
  readonly sizeName: string;
  readonly skuCode: string | null;
  readonly waiting: number;
  readonly oldestRequestAt: string;
}
