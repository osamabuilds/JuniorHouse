import { SectionLink } from '@shared';

/** The three Labelling screens, shown as the section navigation on each of them. */
export const LABELLING_LINKS: readonly SectionLink[] = [
  { label: 'Worklist', path: '/labelling/worklist' },
  { label: 'Print queue', path: '/labelling/queue' },
  { label: 'SKU register', path: '/labelling/register' },
];

/** The reprint reasons (lookup RPRT_RSN_LKP); the ids are stable (FR-SC-07). */
export const REPRINT_REASONS: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Tag lost' },
  { id: 2, label: 'Tag damaged' },
  { id: 3, label: 'Price changed' },
  { id: 4, label: 'Other' },
];

/** How often the queue screen refreshes itself while it is open (milliseconds). */
export const QUEUE_REFRESH_MS = 5000;
