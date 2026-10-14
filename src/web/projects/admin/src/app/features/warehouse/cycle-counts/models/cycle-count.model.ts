/** One count in the list (docs/specs/warehouse/cycle-counts/spec.md, CC-11). */
export interface CycleCountRowDto {
  readonly id: number;
  readonly countNo: string;
  readonly statusId: number;
  /** OPEN, CLOSED or CANCELLED. */
  readonly statusCode: string;
  readonly createdBy: string;
  readonly createdAt: string;
  readonly binCount: number;
  readonly lineCount: number;
}

export interface CountBinDto {
  readonly binId: number;
  readonly binCode: string;
  readonly submitted: boolean;
}

/**
 * What the counter submitted. There is deliberately no system quantity anywhere in this type: the count is blind (CC-2), so the
 * screen cannot show what the system expects while people are counting.
 */
export interface CountLineDto {
  readonly binId: number;
  readonly binCode: string;
  readonly variantRefId: number;
  readonly skuCode: string;
  readonly gtin: string;
  readonly gradeId: number;
  readonly gradeCode: string;
  readonly countedQuantity: number;
}

export interface CycleCountDto {
  readonly id: number;
  readonly countNo: string;
  readonly statusId: number;
  readonly statusCode: string;
  readonly createdBy: string;
  readonly createdAt: string;
  readonly bins: readonly CountBinDto[];
  readonly lines: readonly CountLineDto[];
}

/** The review step (CC-4, CC-7): counted against system, flagged when the bin moved after the count opened. */
export interface CountReviewLineDto {
  readonly binId: number;
  readonly binCode: string;
  readonly variantRefId: number;
  readonly skuCode: string;
  readonly gradeId: number;
  readonly gradeCode: string;
  readonly countedQuantity: number;
  readonly systemQuantity: number;
  readonly variance: number;
  readonly stale: boolean;
  readonly notCounted: boolean;
}

export interface CountReviewDto {
  readonly countId: number;
  readonly countNo: string;
  readonly allBinsSubmitted: boolean;
  readonly lines: readonly CountReviewLineDto[];
}

/** A bin with how long ago it was counted, to help choose what to count next (decision D9). */
export interface BinAgeDto {
  readonly binId: number;
  readonly binCode: string;
  readonly isActive: boolean;
  readonly lastCountedAt: string | null;
  readonly daysSinceCounted: number | null;
  readonly inOpenCount: boolean;
}

export interface CountLineInput {
  readonly gtin: string;
  readonly gradeId: number;
  readonly quantity: number;
}

export interface SubmitLinesValue {
  readonly binCode: string;
  readonly lines: readonly CountLineInput[];
  readonly recount: boolean;
  readonly actingPerson: string;
}

export interface CountReasonInput {
  readonly binId: number;
  readonly variantRefId: number;
  readonly gradeId: number;
  readonly reasonId: number;
  readonly reconfirmed: boolean;
}

/** A chosen reason for one variance line, keyed by bin, variant and grade. */
export interface ReasonChoice {
  readonly reasonId: number;
  readonly reconfirmed: boolean;
}

export type CountStatusFilter = 'all' | 'OPEN' | 'CLOSED' | 'CANCELLED';

/** The key that identifies one review line (bin, variant, grade). */
export function reviewKey(line: { binId: number; variantRefId: number; gradeId: number }): string {
  return `${line.binId}:${line.variantRefId}:${line.gradeId}`;
}
