import { Menu } from '../models/menu.model';

/** A menu shaped like the server's answer for a small catalogue (test and story data only). */
export const sampleMenu: Menu = {
  audiences: [
    {
      code: 'GIRLS',
      name: 'Girls',
      path: '/girls',
      entries: [
        { kind: 'new-in', code: '', label: 'New in', path: '/girls/new-in' },
        { kind: 'sale', code: '', label: 'Sale', path: '/girls/sale' },
        { kind: 'category', code: 'DRESSES', label: 'Dresses', path: '/girls/dresses' },
        { kind: 'collection', code: 'SUMMER', label: 'Summer', path: '/collections/summer' },
        { kind: 'age', code: 'AGE_2Y', label: '2Y', path: '/girls?age=age-2y' },
      ],
    },
    {
      code: 'BOYS',
      name: 'Boys',
      path: '/boys',
      entries: [
        { kind: 'new-in', code: '', label: 'New in', path: '/boys/new-in' },
        { kind: 'category', code: 'TEES_AND_SHIRTS', label: 'Tees and shirts', path: '/boys/tees-and-shirts' },
      ],
    },
  ],
  collections: [{ kind: 'collection', code: 'SUMMER', label: 'Summer', path: '/collections/summer' }],
  hasNewIn: true,
  hasSale: true,
};
