export type RemittanceOutcome = 'MATCHED' | 'SHORT' | 'OVER' | 'DUPLICATE' | 'UNKNOWN' | 'NOT_DELIVERED' | 'NOT_COD';

export interface CourierCodTotals {
  readonly courierName: string;
  readonly awaitingCount: number;
  readonly awaitingCash: number;
  readonly overdueCount: number;
  readonly overdueCash: number;
  readonly settledCount: number;
  readonly settledCash: number;
}

export interface CodSummary {
  readonly couriers: readonly CourierCodTotals[];
  readonly openExceptions: number;
}

export interface CodAwaitingRow {
  readonly shipmentId: number;
  readonly orderNumber: string;
  readonly waybill: string;
  readonly courierName: string;
  readonly cashToCollect: number;
  readonly deliveredAt: string;
  readonly daysWaiting: number;
  readonly overdue: boolean;
}

export interface CodExceptionRow {
  readonly lineId: number;
  readonly remittanceId: number;
  readonly reference: string;
  readonly courierName: string;
  readonly waybill: string;
  readonly amount: number;
  readonly outcome: RemittanceOutcome;
  readonly outcomeName: string;
  readonly orderNumber: string | null;
  readonly expectedCash: number | null;
}

export interface RemittanceSummary {
  readonly id: number;
  readonly courierName: string;
  readonly reference: string;
  readonly remittedOn: string;
  readonly statedTotal: number;
  readonly lineCount: number;
  readonly flaggedCount: number;
  readonly openCount: number;
  readonly recordedAt: string;
  readonly recordedBy: string;
}

export interface RemittanceLine {
  readonly id: number;
  readonly waybill: string;
  readonly amount: number;
  readonly outcome: RemittanceOutcome;
  readonly outcomeName: string;
  readonly orderNumber: string | null;
  readonly expectedCash: number | null;
  readonly resolutionNote: string | null;
  readonly resolvedBy: string | null;
  readonly resolvedAt: string | null;
  readonly isOpen: boolean;
}

export interface RemittanceDetail {
  readonly summary: RemittanceSummary;
  readonly lines: readonly RemittanceLine[];
}

export interface RecordResult {
  readonly remittance: RemittanceDetail;
  readonly alreadyRecorded: boolean;
}

export interface StatementLineInput {
  readonly waybill: string;
  readonly amount: number;
}

export interface RecordRemittanceRequest {
  readonly courier: string;
  readonly reference: string;
  readonly remittedOn: string;
  readonly statedTotal: number;
  readonly lines: readonly StatementLineInput[];
}
