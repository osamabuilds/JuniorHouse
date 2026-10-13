import { LabelWorkItemDto, PrinterDto } from '@features/labelling/label-work';
import { PrintJobDto } from '@features/labelling/print-jobs';
import { GtinCapacityDto, VariantDetailDto, VariantRowDto } from '@features/labelling/variants';

/** Builders for the Labelling DTOs, so each test states only what it cares about. */
export const workItem = (overrides: Partial<LabelWorkItemDto> = {}): LabelWorkItemDto => ({
  id: 1,
  subBatchId: 11,
  qcBatchId: 7,
  poId: 70,
  styleId: 500,
  styleCode: 'STY-DRESS',
  styleName: 'Floral Summer Dress',
  styleIsActive: true,
  sizeId: 1,
  sizeName: '2-3 Years',
  colourId: 10,
  colourName: 'Red',
  qty: 24,
  outcomeCode: 'Accepted',
  statusCode: 'Ready',
  parkReasonCode: null,
  variantId: 31,
  skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
  gtin: '0200000000019',
  printedQty: 0,
  activeQty: 0,
  remainingQty: 24,
  priceChanged: false,
  createdAt: '2026-10-14T09:00:00Z',
  ...overrides,
});

export const printer = (overrides: Partial<PrinterDto> = {}): PrinterDto => ({
  id: 1,
  code: 'FAKE-PDF',
  name: 'Preview printer (no hardware)',
  formatCode: 'Pdf',
  ...overrides,
});

export const printJob = (overrides: Partial<PrintJobDto> = {}): PrintJobDto => ({
  id: 3,
  jobNo: 'PJ-2026-00003',
  kindCode: 'Original',
  statusCode: 'Queued',
  variantGtinId: 21,
  workItemId: 1,
  skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
  gtin: '0200000000019',
  qty: 24,
  priceSnapshot: 1299,
  printerCode: 'FAKE-PDF',
  attemptCount: 0,
  lastError: null,
  reprintReasonCode: null,
  note: null,
  requestedBy: 'Asha',
  manualActionBy: null,
  createdAt: '2026-10-14T09:00:00Z',
  nextAttemptAt: '2026-10-14T09:00:00Z',
  ...overrides,
});

export const variantRow = (overrides: Partial<VariantRowDto> = {}): VariantRowDto => ({
  id: 31,
  styleId: 500,
  styleCode: 'STY-DRESS',
  styleName: 'Floral Summer Dress',
  colourId: 10,
  colourName: 'Red',
  sizeId: 1,
  sizeName: '2-3 Years',
  skuCode: 'ROMP-STY-DRESS-RED-2-3Y',
  activeGtin: '0200000000019',
  retiredCount: 0,
  createdAt: '2026-10-14T09:00:00Z',
  ...overrides,
});

export const variantDetail = (overrides: Partial<VariantDetailDto> = {}): VariantDetailDto => ({
  variant: variantRow(),
  gtins: [
    { id: 22, gtin: '0200000000026', statusCode: 'Active', assignedAt: '2026-10-14T10:00:00Z', retiredAt: null, retiredReasonCode: null, retiredNote: null, retiredBy: null },
    {
      id: 21,
      gtin: '0200000000019',
      statusCode: 'Retired',
      assignedAt: '2026-10-14T09:00:00Z',
      retiredAt: '2026-10-14T09:30:00Z',
      retiredReasonCode: 'Error',
      retiredNote: 'wrong colour on the tag',
      retiredBy: 'Asha',
    },
  ],
  ...overrides,
});

export const capacity = (overrides: Partial<GtinCapacityDto> = {}): GtinCapacityDto => ({
  prefix: '0200000',
  isTestPrefix: true,
  capacity: 100000,
  used: 12,
  remaining: 99987,
  usedPercent: 0,
  warning: false,
  ...overrides,
});
