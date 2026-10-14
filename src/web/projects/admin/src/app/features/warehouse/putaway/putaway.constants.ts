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

/** Where an item is in its journey (lookup PUT_STS_LKP), in words staff use. */
export const PUTAWAY_STATUS_TEXT: Readonly<Record<string, string>> = {
  AWAITING_GTIN: 'Waiting for its barcode',
  PENDING: 'Ready to put away',
  COMPLETE: 'All on the shelf',
};

/** Page sizes: the queue and the history are short lists beside the scan form. */
export const PUTAWAY_PAGE_SIZE = 10;
