import { SectionLink } from '@shared';

/** The three Labelling screens, shown as the section navigation on each of them. */
export const LABELLING_LINKS: readonly SectionLink[] = [
  { label: 'Worklist', path: '/labelling/worklist' },
  { label: 'Print queue', path: '/labelling/queue' },
  { label: 'SKU register', path: '/labelling/register' },
];

/** The reasons for voiding a GTIN (lookup VOID_RSN_LKP); the ids are stable (VAR-8). */
export const VOID_REASONS: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Assigned in error' },
  { id: 2, label: 'Product recalled' },
  { id: 3, label: 'Other' },
];
