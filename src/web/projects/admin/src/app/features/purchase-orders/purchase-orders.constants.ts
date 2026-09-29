/** PO status ids: REF.PO_STS_LKP's seed order (system-owned and stable). */
export const PO_STATUS_DRAFT = 1;
export const PO_STATUS_SENT_TO_VENDOR = 2;
export const PO_STATUS_ACKNOWLEDGED = 3;
export const PO_STATUS_CANCELLED = 4;

/** REF.PO_STS_LKP's seed order (Romp.Modules.Reference.Infrastructure/ReferenceSeedData.cs) - system-owned and stable, so it's safe to mirror here for display. */
export const PO_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Draft',
  2: 'Sent to Vendor',
  3: 'Acknowledged',
  4: 'Cancelled',
};

/** Revision status ids (REF.PO_REV_STS_LKP). */
export const REVISION_STATUS_PENDING = 1;
export const REVISION_STATUS_IN_FORCE = 2;

/** REF.PO_REV_STS_LKP, AMND_INIT_LKP, PO_VNDR_COMM_TYP_LKP: system-owned seeds, mirrored for display like the PO statuses above. */
export const REVISION_STATUS_LABELS: Readonly<Record<number, string>> = {
  1: 'Pending',
  2: 'In force',
  3: 'Superseded',
  4: 'Rejected',
  5: 'Withdrawn',
};

export const INITIATOR_LABELS: Readonly<Record<number, string>> = { 1: 'Romp buyer', 2: 'Vendor' };

export const COMM_TYPE_LABELS: Readonly<Record<number, string>> = {
  1: 'Confirmed',
  2: 'Countered',
  3: 'Declined',
  4: 'Amendment request',
  5: 'Decision',
};

/** Vendor-visible categories are the first five of REF.PO_FILE_CATG_LKP; the rest are internal. */
export const FILE_CATEGORY_LABELS: Readonly<Record<number, string>> = {
  1: 'Tech Pack Spec',
  2: 'Artwork / Labels',
  3: 'Trim Card / BOM',
  4: 'Colour Standard',
  5: 'Packing Instructions',
  6: 'Cost Sheet',
  7: 'Compliance / Test Report',
  8: 'Vendor Evidence',
  9: 'Other',
};

export const TECH_PACK_SPEC = 1;
