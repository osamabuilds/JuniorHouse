import { SectionLink } from '@shared';

/** The Warehouse screens, shown as the section navigation on each of them. */
export const WAREHOUSE_LINKS: readonly SectionLink[] = [
  { label: 'Putaway', path: '/warehouse/putaway' },
  { label: 'Stock', path: '/warehouse/stock' },
  { label: 'Bins', path: '/warehouse/bins' },
  { label: 'Adjustments', path: '/warehouse/adjustments' },
  { label: 'Cycle counts', path: '/warehouse/cycle-counts' },
  { label: 'Alerts', path: '/warehouse/alerts' },
];

/** The grades a counted unit can be (lookup GRD_LKP). */
export const GRADES: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Accepted' },
  { id: 2, label: 'Concession' },
];

/** The text the server starts a large-adjustment refusal with; the review then asks the person to tick "confirm" (ADJ-4b). */
export const RECONFIRM_PREFIX = 'Confirmation required';

/** Words for a count's status (lookup CNT_STS_LKP). */
export const COUNT_STATUS_TEXT: Readonly<Record<string, string>> = {
  OPEN: 'Open',
  CLOSED: 'Closed',
  CANCELLED: 'Cancelled',
};

/** A variance bigger than this many units is asked to be confirmed on the review, mirroring the server's default (decision D5). */
export const LARGE_VARIANCE_HINT = 20;
