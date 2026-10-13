/** DTOs of the Production API (Romp.Modules.Production.Application). Dates are ISO strings (`2026-09-29`). */

export interface QuantityLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
}

/** One row of the production list. */
export interface RunSummaryDto {
  readonly id: number;
  readonly poId: number;
  readonly poNo: string;
  readonly statusId: number;
  readonly fabricResponsibilityId: number | null;
  readonly expectedCompletionDate: string | null;
  readonly latestAcceptableDate: string | null;
  readonly isAtRisk: boolean;
  readonly isCuttingStarted: boolean;
  readonly hasApprovedSample: boolean;
  readonly isCuttingBeforeFabricDispatch: boolean;
  readonly isResampleRecommended: boolean;
  readonly isCuttingWithoutResample: boolean;
  readonly shippedQty: number;
  readonly hasOverDelivery: boolean;
  readonly hasShortDelivery: boolean;
  readonly hasLateDelivery: boolean;
}

export interface MilestoneDto {
  readonly id: number;
  readonly typeId: number;
  readonly typeCode: string;
  readonly claimedDate: string;
  readonly reportedBy: string;
  readonly actingPerson: string;
  readonly note: string | null;
  readonly isRecordedLate: boolean;
  readonly flagReason: string | null;
  readonly ppRoundId: number | null;
  readonly recordedAt: string;
  readonly recordedBy: string;
  readonly finishedLines: readonly QuantityLineDto[];
}

export interface PpSampleFileDto {
  readonly id: number;
  readonly fileName: string;
  readonly contentType: string;
  readonly fileSizeBytes: number;
  readonly uploadedAt: string;
}

export interface PpSampleRoundDto {
  readonly id: number;
  readonly roundNo: number;
  readonly submittedDate: string;
  readonly submittedBy: string;
  readonly note: string | null;
  readonly decisionId: number;
  readonly decisionCode: 'Pending' | 'Approved' | 'Rejected';
  readonly decidedAt: string | null;
  readonly decidedBy: string | null;
  readonly decisionReason: string | null;
  readonly files: readonly PpSampleFileDto[];
}

export interface ExpectedCompletionChangeDto {
  readonly oldDate: string | null;
  readonly newDate: string;
  readonly reason: string | null;
  readonly actingPerson: string;
  readonly changedAt: string;
}

export interface DeliveryNoteLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
  readonly overQty: number;
  readonly shortQty: number;
}

export interface DeliveryNoteDto {
  readonly id: number;
  readonly vendorDnNo: string;
  readonly dispatchDate: string;
  readonly isFinalShipment: boolean;
  readonly revisionNumber: number;
  readonly timingId: number;
  readonly timingCode: 'OnTime' | 'Late' | 'BeyondAcceptable';
  readonly daysLate: number;
  readonly quantityStatusId: number;
  readonly quantityStatusCode: 'OnQuantity' | 'Short' | 'Over';
  readonly overQty: number;
  readonly shortQty: number;
  readonly fabricDispatchDate: string | null;
  readonly isRompFabricDelay: boolean;
  readonly actingPerson: string;
  readonly recordedAt: string;
  readonly lines: readonly DeliveryNoteLineDto[];
}

export interface OrderedVsShippedDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly ordered: number;
  readonly shipped: number;
  readonly minRequired: number;
  readonly maxAllowed: number;
}

/** The PO facts a run screen needs (never the PO's cost, payment terms or internal notes). */
export interface RunPoTermsDto {
  readonly revisionNumber: number;
  readonly expectedDeliveryDate: string;
  readonly latestAcceptableDate: string | null;
  readonly overTolerancePercent: number | null;
  readonly underTolerancePercent: number | null;
}

export interface RunDetailDto {
  readonly id: number;
  readonly poId: number;
  readonly poNo: string;
  readonly statusId: number;
  readonly fabricResponsibilityId: number | null;
  readonly poSentDate: string;
  readonly expectedCompletionDate: string | null;
  readonly latestAcceptableDate: string | null;
  readonly cuttingStartedDate: string | null;
  readonly fabricDispatchDate: string | null;
  readonly isAtRisk: boolean;
  readonly hasApprovedSample: boolean;
  readonly isCuttingBeforeFabricDispatch: boolean;
  readonly isResampleRecommended: boolean;
  readonly isCuttingWithoutResample: boolean;
  readonly canSubmitSample: boolean;
  readonly poTerms: RunPoTermsDto | null;
  readonly milestones: readonly MilestoneDto[];
  readonly ppRounds: readonly PpSampleRoundDto[];
  readonly expectedHistory: readonly ExpectedCompletionChangeDto[];
  readonly deliveryNotes: readonly DeliveryNoteDto[];
  readonly quantities: readonly OrderedVsShippedDto[];
}

export interface RunFilters {
  readonly statusId: number | null;
  readonly search: string;
  readonly atRisk: boolean;
  readonly page: number;
  readonly pageSize: number;
}

// ---- what the forms hand to the store (the acting person is required on every write, D13) ----

export interface RecordMilestoneValue {
  readonly typeId: number;
  readonly claimedDate: string;
  readonly reportedBy: string;
  readonly actingPerson: string;
  readonly note: string | null;
  readonly finishedLines: readonly QuantityLineDto[] | null;
}

export interface SubmitSampleValue {
  readonly submittedDate: string | null;
  readonly reportedBy: string;
  readonly actingPerson: string;
  readonly note: string | null;
}

export interface DecideSampleValue {
  readonly roundNo: number;
  readonly approve: boolean;
  readonly reason: string | null;
  readonly actingPerson: string;
}

export interface ExpectedCompletionValue {
  readonly date: string;
  readonly reason: string | null;
  readonly actingPerson: string;
}

export interface DeliveryNoteValue {
  readonly vendorDnNo: string;
  readonly dispatchDate: string;
  readonly isFinalShipment: boolean;
  readonly lines: readonly QuantityLineDto[];
  readonly actingPerson: string;
}

export interface SampleFileUpload {
  readonly roundNo: number;
  readonly file: File;
}

/** Which action panel of the run screen is open. */
export type ProductionPanel = 'none' | 'milestone' | 'sample' | 'expected' | 'delivery';
