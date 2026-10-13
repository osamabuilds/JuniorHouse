import {
  DeliveryNoteDto,
  MilestoneDto,
  OrderedVsShippedDto,
  PpSampleRoundDto,
  RunDetailDto,
  RunSummaryDto,
} from '@features/production/production-runs';

/** Builders for the Production DTOs, so each test states only what it cares about. */
export const runDetail = (overrides: Partial<RunDetailDto> = {}): RunDetailDto => ({
  id: 5,
  poId: 10,
  poNo: 'PO-2026-00010',
  statusId: 1,
  fabricResponsibilityId: 1,
  poSentDate: '2026-09-01',
  expectedCompletionDate: null,
  latestAcceptableDate: '2026-10-25',
  cuttingStartedDate: null,
  fabricDispatchDate: null,
  isAtRisk: false,
  hasApprovedSample: false,
  isCuttingBeforeFabricDispatch: false,
  isResampleRecommended: false,
  isCuttingWithoutResample: false,
  canSubmitSample: true,
  poTerms: { revisionNumber: 0, expectedDeliveryDate: '2026-10-15', latestAcceptableDate: '2026-10-25', overTolerancePercent: 5, underTolerancePercent: 5 },
  milestones: [],
  ppRounds: [],
  expectedHistory: [],
  deliveryNotes: [],
  quantities: [
    { sizeId: 1, colourId: 10, ordered: 100, shipped: 0, minRequired: 95, maxAllowed: 105 },
    { sizeId: 2, colourId: 10, ordered: 50, shipped: 0, minRequired: 48, maxAllowed: 52 },
  ],
  ...overrides,
});

export const runSummary = (overrides: Partial<RunSummaryDto> = {}): RunSummaryDto => ({
  id: 5,
  poId: 10,
  poNo: 'PO-2026-00010',
  statusId: 1,
  fabricResponsibilityId: 1,
  expectedCompletionDate: null,
  latestAcceptableDate: '2026-10-25',
  isAtRisk: false,
  isCuttingStarted: false,
  hasApprovedSample: false,
  isCuttingBeforeFabricDispatch: false,
  isResampleRecommended: false,
  isCuttingWithoutResample: false,
  shippedQty: 0,
  hasOverDelivery: false,
  hasShortDelivery: false,
  hasLateDelivery: false,
  ...overrides,
});

export const milestone = (overrides: Partial<MilestoneDto> = {}): MilestoneDto => ({
  id: 1,
  typeId: 1,
  typeCode: 'FabricBooked',
  claimedDate: '2026-09-03',
  reportedBy: 'Ali Raza',
  actingPerson: 'Osama Shafique',
  note: null,
  isRecordedLate: false,
  flagReason: null,
  ppRoundId: null,
  recordedAt: '2026-09-03T08:00:00Z',
  recordedBy: 'system',
  finishedLines: [],
  ...overrides,
});

export const round = (overrides: Partial<PpSampleRoundDto> = {}): PpSampleRoundDto => ({
  id: 1,
  roundNo: 1,
  submittedDate: '2026-09-05',
  submittedBy: 'Osama Shafique',
  note: null,
  decisionId: 1,
  decisionCode: 'Pending',
  decidedAt: null,
  decidedBy: null,
  decisionReason: null,
  files: [],
  ...overrides,
});

export const deliveryNote = (overrides: Partial<DeliveryNoteDto> = {}): DeliveryNoteDto => ({
  id: 1,
  vendorDnNo: 'DN-1',
  dispatchDate: '2026-09-25',
  isFinalShipment: false,
  revisionNumber: 0,
  timingId: 1,
  timingCode: 'OnTime',
  daysLate: 0,
  quantityStatusId: 1,
  quantityStatusCode: 'OnQuantity',
  overQty: 0,
  shortQty: 0,
  fabricDispatchDate: null,
  isRompFabricDelay: false,
  actingPerson: 'Osama Shafique',
  recordedAt: '2026-09-25T08:00:00Z',
  lines: [{ sizeId: 1, colourId: 10, qty: 60, overQty: 0, shortQty: 0 }],
  ...overrides,
});

export const quantities = (): OrderedVsShippedDto[] => runDetail().quantities as OrderedVsShippedDto[];

/** Text of a rendered component with runs of whitespace collapsed, for readable assertions. */
export const textOf = (fixture: { nativeElement: unknown }): string => ((fixture.nativeElement as HTMLElement).textContent ?? '').replace(/\s+/g, ' ');

export function setText(root: HTMLElement, selector: string, value: string): void {
  const element = root.querySelector(selector) as HTMLInputElement | HTMLTextAreaElement;
  element.value = value;
  element.dispatchEvent(new Event('input'));
}

export function setSelect(root: HTMLElement, selector: string, index: number): void {
  const select = root.querySelector(selector) as HTMLSelectElement;
  select.selectedIndex = index;
  select.dispatchEvent(new Event('change'));
}

export function click(root: HTMLElement, selector: string): void {
  (root.querySelector(selector) as HTMLElement).click();
}

export function buttonByText(root: HTMLElement, text: string): HTMLButtonElement {
  const found = Array.from(root.querySelectorAll('button')).find((button) => (button.textContent ?? '').includes(text));
  if (!found) {
    throw new Error(`No button containing "${text}"`);
  }
  return found as HTMLButtonElement;
}
