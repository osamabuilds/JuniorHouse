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

/** The text the server starts a large-adjustment refusal with (AdjustmentMessages.ReconfirmPrefix); the screen then asks for confirmation. */
export const RECONFIRM_PREFIX = 'Confirmation required';

/** Which way each reason moves stock (lookup ADJ_RSN_LKP); shown next to the reason so the sign is hard to get wrong (ADJ-3). */
export const REASON_DIRECTION: Readonly<Record<string, string>> = {
  DAMAGE: 'removes units',
  MISCOUNT: 'adds or removes units',
  THEFT: 'removes units',
  FOUND_STOCK: 'adds units',
};

/** The grades a unit of stock can be (lookup GRD_LKP). */
export const GRADES: readonly { readonly id: number; readonly label: string }[] = [
  { id: 1, label: 'Accepted' },
  { id: 2, label: 'Concession' },
];
