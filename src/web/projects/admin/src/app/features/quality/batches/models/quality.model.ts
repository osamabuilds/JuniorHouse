/** DTOs of the Quality API (Romp.Modules.Quality.Application). Dates are ISO strings (`2026-09-29`). */

export interface CountedLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
}

// ---- receiving ----

export interface InboundDeliverySummaryDto {
  readonly id: number;
  readonly runId: number;
  readonly poId: number;
  readonly poNo: string;
  readonly vendorDnNo: string;
  readonly dispatchDate: string;
  readonly isFinalShipment: boolean;
  readonly isCancelled: boolean;
  readonly claimedTotal: number;
  readonly grnId: number | null;
  readonly grnNo: string | null;
}

export interface InboundDeliveryLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly claimedQty: number;
}

export interface InboundDeliveryDto extends InboundDeliverySummaryDto {
  readonly lines: readonly InboundDeliveryLineDto[];
}

export interface GrnLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly claimedQty: number;
  readonly countedQty: number;
  readonly diffQty: number;
  readonly isExcess: boolean;
}

export interface GoodsReceiptDto {
  readonly id: number;
  readonly grnNo: string;
  readonly deliveryId: number;
  readonly runId: number;
  readonly poId: number;
  readonly poNo: string;
  readonly vendorDnNo: string;
  readonly receivedDate: string;
  readonly reconciliationId: number;
  readonly reconciliationCode: 'Matched' | 'Short' | 'Over';
  readonly claimedTotal: number;
  readonly countedTotal: number;
  readonly diffTotal: number;
  readonly actingPerson: string;
  readonly note: string | null;
  readonly lines: readonly GrnLineDto[];
}

// ---- QC ----

export interface QcBatchSummaryDto {
  readonly id: number;
  readonly grnId: number;
  readonly grnNo: string;
  readonly poId: number;
  readonly poNo: string;
  readonly vendorDnNo: string;
  readonly statusId: number;
  readonly lotQty: number;
  readonly latestResultId: number | null;
}

export interface BatchLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
}

export interface CheckResultDto {
  readonly checkTypeId: number;
  readonly resultId: number;
}

export interface DefectDto {
  readonly defectTypeId: number;
  readonly severityId: number;
  readonly qty: number;
  readonly sizeId: number | null;
  readonly colourId: number | null;
  readonly note: string;
}

export interface SafetyLineDto {
  readonly sizeId: number;
  readonly colourId: number;
}

export interface SafetyCheckDto {
  readonly safetyTypeId: number;
  readonly resultId: number;
  readonly reason: string | null;
  readonly affectedLines: readonly SafetyLineDto[];
}

export interface InspectionDto {
  readonly id: number;
  readonly batchId: number;
  readonly subBatchId: number | null;
  readonly roundNo: number;
  readonly modeId: number;
  readonly modeCode: 'Aql' | 'Full';
  readonly inspectionLevelId: number;
  readonly aqlLevelId: number;
  readonly lotQty: number;
  readonly sampleQty: number;
  readonly acceptNo: number;
  readonly rejectNo: number;
  readonly resultId: number;
  readonly resultCode: 'Pass' | 'Fail';
  readonly inspectionDate: string;
  readonly actingPerson: string;
  readonly checks: readonly CheckResultDto[];
  readonly defects: readonly DefectDto[];
  readonly safetyChecks: readonly SafetyCheckDto[];
}

export interface SubBatchLineDto {
  readonly sizeId: number;
  readonly colourId: number;
  readonly qty: number;
  readonly returnedQty: number;
  readonly outstandingQty: number;
}

export interface ReworkReturnDto {
  readonly returnDate: string;
  readonly actingPerson: string;
  readonly note: string | null;
}

export interface SubBatchDto {
  readonly id: number;
  readonly splitNo: number;
  readonly parentSubBatchId: number | null;
  readonly outcomeId: number | null;
  readonly outcomeCode: string | null;
  readonly reason: string | null;
  readonly statusId: number;
  readonly reworkRoundCount: number;
  readonly decidedAt: string | null;
  readonly actingPerson: string | null;
  readonly lines: readonly SubBatchLineDto[];
  readonly returns: readonly ReworkReturnDto[];
}

export interface QcBatchDto {
  readonly id: number;
  readonly grnId: number;
  readonly grnNo: string;
  readonly vendorDnNo: string;
  readonly poId: number;
  readonly poNo: string;
  readonly statusId: number;
  readonly lotQty: number;
  readonly lines: readonly BatchLineDto[];
  readonly inspections: readonly InspectionDto[];
  readonly subBatches: readonly SubBatchDto[];
}

export interface AqlPlanDto {
  readonly modeId: number;
  readonly modeCode: 'Aql' | 'Full';
  readonly codeLetter: string;
  readonly lotQty: number;
  readonly sampleQty: number;
  readonly acceptNo: number;
  readonly rejectNo: number;
}

export interface BatchDecisionDto {
  readonly batchId: number;
  readonly batchStatusId: number;
  readonly subBatches: readonly SubBatchDto[];
}

export interface ReworkReturnResultDto {
  readonly batchId: number;
  readonly batchStatusId: number;
  readonly reworkPart: SubBatchDto;
  readonly returnedPart: SubBatchDto;
}

// ---- lists ----

export interface DeliveryFilters {
  readonly status: string | null;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

export interface BatchFilters {
  readonly statusId: number | null;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

// ---- what the forms hand to the store (the acting person is required on every write) ----

export interface RecordReceiptValue {
  readonly inboundDeliveryId: number;
  readonly receivedDate: string;
  readonly actingPerson: string;
  readonly note: string | null;
  readonly lines: readonly CountedLineDto[];
}

export interface RecordInspectionValue {
  readonly subBatchId: number | null;
  readonly inspectionDate: string;
  readonly actingPerson: string;
  readonly sampleQty: number | null;
  readonly checks: readonly CheckResultDto[];
  readonly defects: readonly DefectDto[];
}

export interface RecordSafetyValue {
  readonly inspectionId: number;
  readonly actingPerson: string;
  readonly checks: readonly SafetyCheckDto[];
}

export interface DecisionPartValue {
  readonly outcomeId: number;
  readonly reason: string | null;
  /** Null means "every line of the batch" (only for a decision made in one part). */
  readonly lines: readonly CountedLineDto[] | null;
}

export interface DecideBatchValue {
  readonly actingPerson: string;
  readonly parts: readonly DecisionPartValue[];
}

export interface DecideSubBatchValue {
  readonly subBatchId: number;
  readonly outcomeId: number;
  readonly reason: string | null;
  readonly actingPerson: string;
}

export interface ReworkReturnValue {
  readonly subBatchId: number;
  readonly returnDate: string;
  readonly actingPerson: string;
  readonly note: string | null;
  readonly lines: readonly CountedLineDto[];
}

/** Which action form the batch page is showing, if any. */
export type QualityPanel = 'none' | 'inspect' | 'safety' | 'decide' | 'return' | 'reinspect' | 'decide-part';
