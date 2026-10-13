import { SectionLink } from '@shared';

/** The three Labelling screens, shown as the section navigation on each of them. */
export const LABELLING_LINKS: readonly SectionLink[] = [
  { label: 'Worklist', path: '/labelling/worklist' },
  { label: 'Print queue', path: '/labelling/queue' },
  { label: 'SKU register', path: '/labelling/register' },
];

/** What a parked item is waiting for, in words staff can act on (LW-4). */
export const PARK_REASON_TEXT: Readonly<Record<string, string>> = {
  NoStyle: "The PO's style could not be found.",
  StyleRetired: 'The style is retired. Confirm to label the goods that were received.',
  UnknownCode: 'A colour or size code could not be found.',
};
