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

/** How often the navigation badge asks how many alerts are open while the admin app is on screen. */
export const BADGE_REFRESH_MS = 60_000;

export const MAX_THRESHOLD = 100_000;
