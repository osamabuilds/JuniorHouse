import { Page } from '@shared';
import { AdjustmentDto, AdjustmentReasonDto, PostAdjustmentValue } from '@features/warehouse/adjustments';
import { BinRowDto } from '@features/warehouse/bins';
import { BinAgeDto, CountReviewDto, CountReviewLineDto, CycleCountDto, CycleCountRowDto } from '@features/warehouse/cycle-counts';
import { PutawayItemRowDto, PutawayRecordDto, PutawaySelection, ScanLookupDto } from '@features/warehouse/putaway';
import { AlertRowDto, ThresholdRowDto } from '@features/warehouse/stock-alerts';
import { StockMovementDto, StockVariantRowDto } from '@features/warehouse/stock-ledger';

/** A one-page result, for reducer and effect specs. */
export function pageOf<T>(items: readonly T[], overrides: Partial<Page<T>> = {}): Page<T> {
  return { items, page: 1, pageSize: 10, totalCount: items.length, totalPages: 1, ...overrides };
}

export function stockRow(overrides: Partial<StockVariantRowDto> = {}): StockVariantRowDto {
  return {
    variantRefId: 31,
    variantId: 301,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    gtin: '0200000000019',
    gtinRetired: false,
    onHand: 13,
    available: 13,
    onHandAccepted: 10,
    onHandConcession: 3,
    bins: [
      { binId: 5, binCode: 'A-03-2', gradeId: 1, gradeCode: 'ACCEPTED', quantity: 10 },
      { binId: 6, binCode: 'B-01-1', gradeId: 2, gradeCode: 'CONCESSION', quantity: 3 },
    ],
    ...overrides,
  };
}

export function movement(overrides: Partial<StockMovementDto> = {}): StockMovementDto {
  return {
    id: 900,
    variantRefId: 31,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    binId: 5,
    binCode: 'A-03-2',
    gradeCode: 'ACCEPTED',
    kindCode: 'PUTAWAY',
    referenceId: 12,
    quantityChange: 10,
    balanceBefore: 0,
    balanceAfter: 10,
    actorName: 'Ayesha Khan',
    at: '2026-10-14T09:00:00Z',
    ...overrides,
  };
}

export function putawayItem(overrides: Partial<PutawayItemRowDto> = {}): PutawayItemRowDto {
  return {
    id: 12,
    subBatchId: 40,
    poId: 7001,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    gtin: '2000000000015',
    gtinRetired: false,
    gradeId: 1,
    gradeCode: 'ACCEPTED',
    qcQuantity: 10,
    putQuantity: 4,
    pending: 6,
    statusId: 2,
    statusCode: 'PENDING',
    receivedAt: '2026-10-14T08:00:00Z',
    ...overrides,
  };
}

export function scanLookup(overrides: Partial<ScanLookupDto> = {}): ScanLookupDto {
  return {
    variantRefId: 31,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    gtin: '2000000000015',
    gtinRetired: false,
    pendingItems: [{ itemId: 12, poId: 7001, gradeId: 1, gradeCode: 'ACCEPTED', pending: 6 }],
    ...overrides,
  };
}

export function putawayRecord(overrides: Partial<PutawayRecordDto> = {}): PutawayRecordDto {
  return {
    id: 70,
    itemId: 12,
    binId: 5,
    binCode: 'A-03-2',
    quantity: 4,
    scanned: true,
    actorName: 'Ayesha Khan',
    at: '2026-10-14T09:30:00Z',
    itemPending: 6,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    ...overrides,
  };
}

export function selection(overrides: Partial<PutawaySelection> = {}): PutawaySelection {
  return {
    itemId: 12,
    label: 'ROMP-STY-DRESS-RED-2-3Y (PO 7001)',
    pending: 6,
    gradeCode: 'ACCEPTED',
    scanned: true,
    gtin: '2000000000015',
    idempotencyKey: '11111111-1111-4111-8111-111111111111',
    ...overrides,
  };
}

export function adjustment(overrides: Partial<AdjustmentDto> = {}): AdjustmentDto {
  return {
    id: 80,
    variantRefId: 31,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    gtin: '2000000000015',
    binId: 5,
    binCode: 'A-03-2',
    gradeId: 1,
    gradeCode: 'ACCEPTED',
    quantityChange: -3,
    reasonId: 1,
    reasonCode: 'DAMAGE',
    note: 'crushed in the aisle',
    reconfirmed: false,
    balanceBefore: 10,
    balanceAfter: 7,
    actorName: 'Ayesha Khan',
    at: '2026-10-14T10:00:00Z',
    ...overrides,
  };
}

export const REASONS: readonly AdjustmentReasonDto[] = [
  { id: 1, code: 'DAMAGE', name: 'Damage' },
  { id: 2, code: 'MISCOUNT', name: 'Miscount' },
  { id: 3, code: 'THEFT', name: 'Theft' },
  { id: 4, code: 'FOUND_STOCK', name: 'Found stock' },
];

export function adjustmentValue(overrides: Partial<PostAdjustmentValue> = {}): PostAdjustmentValue {
  return {
    gtin: '2000000000015',
    binCode: 'A-03-2',
    gradeId: 1,
    quantityChange: -30,
    reasonId: 3,
    note: null,
    reconfirmed: false,
    idempotencyKey: '22222222-2222-4222-8222-222222222222',
    actingPerson: 'Ayesha Khan',
    ...overrides,
  };
}

export function alertRow(overrides: Partial<AlertRowDto> = {}): AlertRowDto {
  return {
    id: 90,
    variantRefId: 31,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    gtin: '2000000000015',
    levelId: 1,
    levelCode: 'LOW',
    available: 3,
    threshold: 5,
    openedAt: '2026-10-14T11:00:00Z',
    acknowledged: false,
    acknowledgedBy: null,
    acknowledgedAt: null,
    ...overrides,
  };
}

export function thresholdRow(overrides: Partial<ThresholdRowDto> = {}): ThresholdRowDto {
  return {
    variantRefId: 31,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    gtin: '2000000000015',
    available: 3,
    effectiveThreshold: 5,
    isOverride: false,
    ...overrides,
  };
}

export function countRow(overrides: Partial<CycleCountRowDto> = {}): CycleCountRowDto {
  return {
    id: 60,
    countNo: 'CC-2026-0001',
    statusId: 1,
    statusCode: 'OPEN',
    createdBy: 'Ayesha Khan',
    createdAt: '2026-10-14T08:00:00Z',
    binCount: 2,
    lineCount: 3,
    ...overrides,
  };
}

export function countDto(overrides: Partial<CycleCountDto> = {}): CycleCountDto {
  return {
    id: 60,
    countNo: 'CC-2026-0001',
    statusId: 1,
    statusCode: 'OPEN',
    createdBy: 'Ayesha Khan',
    createdAt: '2026-10-14T08:00:00Z',
    bins: [
      { binId: 5, binCode: 'A-03-2', submitted: true },
      { binId: 6, binCode: 'B-01-1', submitted: false },
    ],
    lines: [
      { binId: 5, binCode: 'A-03-2', variantRefId: 31, skuCode: 'ROMP-STY-DRESS-RED-2-3Y', gtin: '2000000000015', gradeId: 1, gradeCode: 'ACCEPTED', countedQuantity: 8 },
    ],
    ...overrides,
  };
}

export function reviewLine(overrides: Partial<CountReviewLineDto> = {}): CountReviewLineDto {
  return {
    binId: 5,
    binCode: 'A-03-2',
    variantRefId: 31,
    skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
    gradeId: 1,
    gradeCode: 'ACCEPTED',
    countedQuantity: 8,
    systemQuantity: 10,
    variance: -2,
    stale: false,
    notCounted: false,
    ...overrides,
  };
}

export function review(lines: CountReviewLineDto[] = [reviewLine()], overrides: Partial<CountReviewDto> = {}): CountReviewDto {
  return { countId: 60, countNo: 'CC-2026-0001', allBinsSubmitted: true, lines, ...overrides };
}

export function binAge(overrides: Partial<BinAgeDto> = {}): BinAgeDto {
  return { binId: 5, binCode: 'A-03-2', isActive: true, lastCountedAt: null, daysSinceCounted: null, inOpenCount: false, ...overrides };
}

export function binRow(overrides: Partial<BinRowDto> = {}): BinRowDto {
  return {
    id: 5,
    binCode: 'A-03-2',
    zone: 'A',
    aisle: '03',
    shelf: '2',
    isActive: true,
    unitsOnHand: 0,
    lastCountedAt: null,
    ...overrides,
  };
}
