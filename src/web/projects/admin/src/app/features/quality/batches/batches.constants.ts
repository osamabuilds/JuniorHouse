/** QC lookup ids (system-owned, stable: QC.QC_OTCM_LKP and friends). */

export const OUTCOME_ACCEPTED = 1;
export const OUTCOME_CONCESSION = 2;
export const OUTCOME_REJECTED = 3;
export const OUTCOME_REWORK = 4;

export const OUTCOME_LABELS: Readonly<Record<number, string>> = {
  1: 'Accept',
  2: 'Accept with concession',
  3: 'Reject and return to vendor',
  4: 'Rework (send back to the vendor)',
};

/** What a decided part is called once it is decided. */
export const OUTCOME_RESULT_LABELS: Readonly<Record<number, string>> = {
  1: 'Accepted',
  2: 'Accepted with concession',
  3: 'Rejected',
  4: 'Sent back for rework',
};

/** Outcomes that need a written reason (spec AC-20). */
export const OUTCOMES_NEEDING_REASON: readonly number[] = [OUTCOME_CONCESSION, OUTCOME_REJECTED, OUTCOME_REWORK];

/** QC.QC_BTCH_STS_LKP. */
export const BATCH_PENDING = 1;
export const BATCH_IN_INSPECTION = 2;
export const BATCH_DECIDED = 3;
export const BATCH_AWAITING_REWORK = 4;
export const BATCH_CLOSED = 5;

export const BATCH_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Waiting for inspection',
  2: 'In inspection',
  3: 'Decided',
  4: 'Waiting for rework',
  5: 'Closed',
};

/** Part statuses use the same lookup, worded for a part. */
export const PART_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Waiting for re-inspection',
  2: 'Inspected, waiting for a decision',
  3: 'Decided',
  4: 'With the vendor for rework',
  5: 'Closed',
};

/** QC.GRN_RCNL_LKP. */
export const RECONCILIATION_LABELS: Readonly<Record<number, string>> = { 1: 'Matched', 2: 'Short', 3: 'Over' };

/** QC.QC_CHK_TYP_LKP (the six tech-pack checks, in the order staff work through them). */
export const CHECK_TYPES: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Stitching' },
  { id: 2, label: 'Measurements against the size chart' },
  { id: 3, label: 'Fabric and colour match the approved sample' },
  { id: 4, label: 'Trims, buttons and zips secure' },
  { id: 5, label: 'No visible defects' },
  { id: 6, label: 'Care label content correct' },
];

export const CHECK_PASS = 1;
export const CHECK_FAIL = 2;
export const CHECK_NOT_APPLICABLE = 3;

export const CHECK_RESULT_LABELS: Readonly<Record<number, string>> = { 1: 'Pass', 2: 'Fail', 3: 'Not applicable' };

/** QC.QC_SAFE_TYP_LKP: the two child-safety checks that must be recorded before anything is accepted (BRD 5.11). */
export const SAFETY_TYPES: readonly { readonly id: number; readonly label: string; readonly hint: string }[] = [
  { id: 1, label: 'Choking hazard on small trims', hint: 'Buttons, bows and other small parts that a child could pull off and swallow.' },
  { id: 2, label: 'Drawstring safety on necklines', hint: 'Drawstrings or cords around the neck or hood.' },
];

/** QC.QC_DFCT_TYP_LKP. The first six match the checklist items one to one. */
export const DEFECT_TYPES: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Stitching' },
  { id: 2, label: 'Measurement' },
  { id: 3, label: 'Fabric or colour' },
  { id: 4, label: 'Trims not secure' },
  { id: 5, label: 'Visible defect' },
  { id: 6, label: 'Care label' },
  { id: 7, label: 'Other' },
];

/** The defect type a failed checklist item requires (spec AC-17). */
export const DEFECT_TYPE_FOR_CHECK: Readonly<Record<number, number>> = { 1: 1, 2: 2, 3: 3, 4: 4, 5: 5, 6: 6 };

export const SEVERITIES: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Critical' },
  { id: 2, label: 'Major' },
  { id: 3, label: 'Minor' },
];

export const INSPECTION_RESULT_LABELS: Readonly<Record<string, string>> = { Pass: 'Pass', Fail: 'Fail' };

export const MODE_LABELS: Readonly<Record<string, string>> = { Aql: 'AQL sampling', Full: '100% inspection' };

/** Delivery queue filters (the API's status words). */
export const DELIVERY_STATUS_OPTIONS: readonly { readonly value: string; readonly label: string }[] = [
  { value: 'open', label: 'Waiting to be received' },
  { value: 'received', label: 'Received' },
  { value: 'cancelled', label: 'Purchase order cancelled' },
];
