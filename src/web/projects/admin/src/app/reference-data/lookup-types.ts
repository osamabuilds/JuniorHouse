export interface LookupTypeConfig {
  readonly key: string;
  readonly label: string;
  readonly mutable: boolean;
  readonly hasParent?: boolean;
  readonly hasAdvancePercent?: boolean;
}

/** The staff-facing REF lookups (spec AC-1) - po-statuses is system-owned, no create/edit/retire (AC-1). */
export const LOOKUP_TYPES: readonly LookupTypeConfig[] = [
  { key: 'sizes', label: 'Sizes', mutable: true },
  { key: 'colours', label: 'Colours', mutable: true },
  { key: 'fabrics', label: 'Fabrics', mutable: true },
  { key: 'genders', label: 'Genders', mutable: true },
  { key: 'age-brackets', label: 'Age Brackets', mutable: true },
  { key: 'categories', label: 'Categories', mutable: true, hasParent: true },
  { key: 'cities', label: 'Cities', mutable: true },
  { key: 'payment-terms', label: 'Payment Terms', mutable: true, hasAdvancePercent: true },
  { key: 'vendor-specialisations', label: 'Vendor Specialisations', mutable: true },
  { key: 'po-cancel-reasons', label: 'PO Cancel Reasons', mutable: true },
  // Sprint 2 (SCRUM-93, task 54). The five system-owned Sprint 2 lookups (revision statuses,
  // initiators, communication types, file categories, fabric responsibilities) are not listed:
  // they drive code branches, so staff can't edit them.
  { key: 'amendment-reasons', label: 'Amendment Reasons', mutable: true },
  { key: 'vendor-comm-channels', label: 'Vendor Communication Channels', mutable: true },
  { key: 'po-statuses', label: 'PO Statuses', mutable: false },
];
