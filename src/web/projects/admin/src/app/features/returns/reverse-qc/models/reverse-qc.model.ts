/** Shapes of the reverse-QC API (docs/specs/returns/reverse-qc). Ids, quantities and codes only: no customer data (RQC-13). */

export type ReturnStatus = 'AWAITING_RECEIPT' | 'IN_INSPECTION' | 'COMPLETED';
export type ReturnDecision = 'RESTOCK' | 'SECONDS' | 'REJECT';

export interface ReturnCaseSummary {
  readonly id: number;
  readonly orderId: number;
  readonly orderNumber: string;
  readonly status: ReturnStatus;
  readonly lineCount: number;
  readonly decidedCount: number;
  readonly openedAt: string;
  readonly completedAt: string | null;
}

export interface ReturnLine {
  readonly id: number;
  readonly variantId: number;
  readonly sku: string | null;
  readonly name: string;
  readonly colour: string;
  readonly size: string;
  readonly orderedQuantity: number;
  readonly decision: ReturnDecision | null;
  readonly decidedQuantity: number | null;
  readonly note: string | null;
  readonly decidedBy: string | null;
  readonly decidedAt: string | null;
}

export interface ReturnCaseDetail {
  readonly id: number;
  readonly orderId: number;
  readonly orderNumber: string;
  readonly status: ReturnStatus;
  readonly sealIntact: boolean | null;
  readonly receivingBinCode: string | null;
  readonly receivedBy: string | null;
  readonly receivedAt: string | null;
  readonly completedBy: string | null;
  readonly completedAt: string | null;
  readonly openedAt: string;
  readonly lines: readonly ReturnLine[];
}

export interface ReceiveRequest {
  readonly sealIntact: boolean;
  readonly binCode: string;
}

export interface DecideRequest {
  readonly decision: ReturnDecision;
  readonly quantity: number;
  readonly note: string | null;
}

export const STATUS_FILTERS: readonly { readonly code: ReturnStatus | ''; readonly label: string }[] = [
  { code: '', label: 'All cases' },
  { code: 'AWAITING_RECEIPT', label: 'Awaiting receipt' },
  { code: 'IN_INSPECTION', label: 'In inspection' },
  { code: 'COMPLETED', label: 'Completed' },
];

const statusWords: Record<ReturnStatus, string> = {
  AWAITING_RECEIPT: 'Awaiting receipt',
  IN_INSPECTION: 'In inspection',
  COMPLETED: 'Completed',
};

const decisionWords: Record<ReturnDecision, string> = {
  RESTOCK: 'Restocked',
  SECONDS: 'Restocked as seconds',
  REJECT: 'Rejected',
};

export const statusWord = (status: ReturnStatus): string => statusWords[status];

export const decisionWord = (decision: ReturnDecision): string => decisionWords[decision];
