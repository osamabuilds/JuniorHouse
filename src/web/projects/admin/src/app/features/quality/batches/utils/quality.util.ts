import { BatchLineDto, CountedLineDto, InspectionDto, QcBatchDto, SubBatchDto } from '../models';
import { BATCH_AWAITING_REWORK, BATCH_DECIDED, BATCH_IN_INSPECTION, BATCH_PENDING } from '../batches.constants';

/** Today as an ISO date in Pakistan time (UTC+5, no daylight saving): the day a receipt or inspection is being recorded. */
export function todayInPakistan(now: Date = new Date()): string {
  return new Date(now.getTime() + 5 * 60 * 60 * 1000).toISOString().slice(0, 10);
}

export function lineKey(sizeId: number, colourId: number): string {
  return `${sizeId}-${colourId}`;
}

export interface ReconciliationPreview {
  readonly code: 'Matched' | 'Short' | 'Over';
  readonly claimedTotal: number;
  readonly countedTotal: number;
  readonly diffTotal: number;
}

/**
 * The same rule the API applies (spec AC-7), shown live while counting: Matched when every line equals its claim; Short when
 * some line is short and none over; Over when some line is over and none short; when lines differ both ways the totals decide,
 * and an equal total counts as Short. The API's answer is the one that is stored.
 */
export function previewReconciliation(lines: readonly { readonly claimedQty: number; readonly countedQty: number }[]): ReconciliationPreview {
  const claimedTotal = lines.reduce((sum, l) => sum + l.claimedQty, 0);
  const countedTotal = lines.reduce((sum, l) => sum + l.countedQty, 0);
  const anyShort = lines.some((l) => l.countedQty < l.claimedQty);
  const anyOver = lines.some((l) => l.countedQty > l.claimedQty);

  let code: ReconciliationPreview['code'];
  if (!anyShort && !anyOver) {
    code = 'Matched';
  } else if (anyShort && !anyOver) {
    code = 'Short';
  } else if (anyOver && !anyShort) {
    code = 'Over';
  } else {
    code = countedTotal > claimedTotal ? 'Over' : 'Short';
  }

  return { code, claimedTotal, countedTotal, diffTotal: countedTotal - claimedTotal };
}

/** How much of each size and colour the parts of a split have taken so far, against what the batch has (spec AC-22). */
export interface SplitLineBalance {
  readonly sizeId: number;
  readonly colourId: number;
  readonly batchQty: number;
  readonly assigned: number;
  /** Positive: still to place in a part. Negative: too many placed. */
  readonly remaining: number;
}

export function splitBalance(batchLines: readonly BatchLineDto[], parts: readonly (readonly CountedLineDto[])[]): readonly SplitLineBalance[] {
  return batchLines.map((line) => {
    const assigned = parts.reduce(
      (sum, part) => sum + part.filter((l) => l.sizeId === line.sizeId && l.colourId === line.colourId).reduce((s, l) => s + l.qty, 0),
      0,
    );
    return { sizeId: line.sizeId, colourId: line.colourId, batchQty: line.qty, assigned, remaining: line.qty - assigned };
  });
}

export function splitAddsUp(balance: readonly SplitLineBalance[]): boolean {
  return balance.every((line) => line.remaining === 0);
}

/** The first pass (whole batch) inspection, if it has been recorded. */
export function firstInspection(batch: QcBatchDto): InspectionDto | null {
  return batch.inspections.find((i) => i.subBatchId === null) ?? null;
}

/** The latest inspection of a returned part. */
export function inspectionOfPart(batch: QcBatchDto, subBatch: SubBatchDto): InspectionDto | null {
  return batch.inspections.filter((i) => i.subBatchId === subBatch.id).sort((a, b) => b.roundNo - a.roundNo)[0] ?? null;
}

/** True once both safety checks have a result on the inspection (BRD 5.11 non-negotiable, spec AC-31). */
export function hasBothSafetyChecks(inspection: InspectionDto | null): boolean {
  return inspection !== null && inspection.safetyChecks.length >= 2;
}

/** A short plain-language line saying what the batch needs next. */
export function nextStep(batch: QcBatchDto): string {
  const first = firstInspection(batch);
  if (batch.subBatches.length === 0) {
    if (first === null) {
      return 'Inspect the batch: record the checklist and any defects.';
    }
    if (!hasBothSafetyChecks(first)) {
      return 'Record both child-safety checks. Nothing can be accepted until they are recorded.';
    }
    return 'Record the outcome for the whole batch, or split it if only part failed.';
  }
  if (batch.statusId === BATCH_DECIDED) {
    return 'Every part has been decided.';
  }
  if (batch.statusId === BATCH_AWAITING_REWORK) {
    return 'Parts are still with the vendor or waiting to be re-inspected and decided.';
  }
  return batch.statusId === BATCH_PENDING || batch.statusId === BATCH_IN_INSPECTION ? 'Inspect the batch.' : '';
}

/** Pieces of a rework part the vendor has not yet returned. */
export function outstandingQty(subBatch: SubBatchDto): number {
  return subBatch.lines.reduce((sum, l) => sum + l.outstandingQty, 0);
}
