import {
  GoodsReceiptDto,
  InboundDeliveryDto,
  InboundDeliverySummaryDto,
  InspectionDto,
  QcBatchDto,
  QcBatchSummaryDto,
  SubBatchDto,
} from '@features/quality/batches';

/** Builders for the Quality DTOs, so each test states only what it cares about. */
export const inboundSummary = (overrides: Partial<InboundDeliverySummaryDto> = {}): InboundDeliverySummaryDto => ({
  id: 11,
  runId: 5,
  poId: 10,
  poNo: 'PO-2026-00010',
  vendorDnNo: 'DN-1',
  dispatchDate: '2026-09-20',
  isFinalShipment: false,
  isCancelled: false,
  claimedTotal: 150,
  grnId: null,
  grnNo: null,
  ...overrides,
});

export const inboundDelivery = (overrides: Partial<InboundDeliveryDto> = {}): InboundDeliveryDto => ({
  ...inboundSummary(),
  lines: [
    { sizeId: 1, colourId: 10, claimedQty: 100 },
    { sizeId: 2, colourId: 10, claimedQty: 50 },
  ],
  ...overrides,
});

export const receipt = (overrides: Partial<GoodsReceiptDto> = {}): GoodsReceiptDto => ({
  id: 31,
  grnNo: 'GRN-2026-00001',
  deliveryId: 11,
  runId: 5,
  poId: 10,
  poNo: 'PO-2026-00010',
  vendorDnNo: 'DN-1',
  receivedDate: '2026-09-25',
  reconciliationId: 2,
  reconciliationCode: 'Short',
  claimedTotal: 150,
  countedTotal: 140,
  diffTotal: -10,
  actingPerson: 'Sara Khan',
  note: null,
  lines: [
    { sizeId: 1, colourId: 10, claimedQty: 100, countedQty: 90, diffQty: -10, isExcess: false },
    { sizeId: 2, colourId: 10, claimedQty: 50, countedQty: 50, diffQty: 0, isExcess: false },
  ],
  ...overrides,
});

export const batchSummary = (overrides: Partial<QcBatchSummaryDto> = {}): QcBatchSummaryDto => ({
  id: 41,
  grnId: 31,
  grnNo: 'GRN-2026-00001',
  poId: 10,
  poNo: 'PO-2026-00010',
  vendorDnNo: 'DN-1',
  statusId: 1,
  lotQty: 140,
  latestResultId: null,
  ...overrides,
});

export const inspection = (overrides: Partial<InspectionDto> = {}): InspectionDto => ({
  id: 51,
  batchId: 41,
  subBatchId: null,
  roundNo: 1,
  modeId: 1,
  modeCode: 'Aql',
  inspectionLevelId: 6,
  aqlLevelId: 4,
  lotQty: 140,
  sampleQty: 20,
  acceptNo: 1,
  rejectNo: 2,
  resultId: 1,
  resultCode: 'Pass',
  inspectionDate: '2026-09-26',
  actingPerson: 'Ali Raza',
  checks: [1, 2, 3, 4, 5, 6].map((checkTypeId) => ({ checkTypeId, resultId: 1 })),
  defects: [],
  safetyChecks: [],
  ...overrides,
});

export const bothSafetyPassed = () => [
  { safetyTypeId: 1, resultId: 1, reason: null, affectedLines: [] },
  { safetyTypeId: 2, resultId: 1, reason: null, affectedLines: [] },
];

export const subBatch = (overrides: Partial<SubBatchDto> = {}): SubBatchDto => ({
  id: 61,
  splitNo: 1,
  parentSubBatchId: null,
  outcomeId: 1,
  outcomeCode: 'Accepted',
  reason: null,
  statusId: 3,
  reworkRoundCount: 0,
  decidedAt: '2026-09-27T10:00:00+00:00',
  actingPerson: 'Ali Raza',
  lines: [{ sizeId: 1, colourId: 10, qty: 90, returnedQty: 0, outstandingQty: 0 }],
  returns: [],
  ...overrides,
});

export const qcBatch = (overrides: Partial<QcBatchDto> = {}): QcBatchDto => ({
  id: 41,
  grnId: 31,
  grnNo: 'GRN-2026-00001',
  vendorDnNo: 'DN-1',
  poId: 10,
  poNo: 'PO-2026-00010',
  statusId: 1,
  lotQty: 140,
  lines: [
    { sizeId: 1, colourId: 10, qty: 90 },
    { sizeId: 2, colourId: 10, qty: 50 },
  ],
  inspections: [],
  subBatches: [],
  ...overrides,
});
