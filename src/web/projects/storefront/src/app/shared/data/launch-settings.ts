import { NavItem, PageSummary, SiteSettings } from '../models/site-settings.model';

/**
 * The built-in launch set: the same shape the settings API returns (SCRUM-206), used until that API exists and as the
 * last resort when it is unreachable. Placeholder category paths stay valid until Catalog has its category tree.
 * It holds no business facts (delivery, exchange): those are entered in the admin app.
 */
const link = (key: string, label: string, path: string, sort: number, children: readonly NavItem[] = []): NavItem => ({
  key, label, kind: children.length > 0 ? 'group' : 'link', target: { kind: 'path', path }, enabled: true, sort, children,
});

const girls: NavItem = {
  showAges: true,
  ...link('girls', 'Girls', '/girls', 1, [
    link('girls-dresses', 'Dresses', '/girls/dresses', 1),
    link('girls-tops', 'Tops and tees', '/girls/tops-and-tees', 2, [
      link('girls-tees', 'T-shirts', '/girls/tops-and-tees/t-shirts', 1),
      link('girls-blouses', 'Blouses', '/girls/tops-and-tees/blouses', 2),
    ]),
    link('girls-bottoms', 'Bottoms', '/girls/bottoms', 3, [
      link('girls-skirts', 'Skirts', '/girls/bottoms/skirts', 1),
      link('girls-trousers', 'Trousers', '/girls/bottoms/trousers', 2),
    ]),
  ]),
};

const boys: NavItem = {
  showAges: true,
  ...link('boys', 'Boys', '/boys', 2, [
    link('boys-tees', 'Tees and shirts', '/boys/tees-and-shirts', 1, [
      link('boys-t-shirts', 'T-shirts', '/boys/tees-and-shirts/t-shirts', 1),
      link('boys-polos', 'Polo shirts', '/boys/tees-and-shirts/polo-shirts', 2),
    ]),
    link('boys-bottoms', 'Bottoms', '/boys/bottoms', 2, [
      link('boys-jeans', 'Jeans', '/boys/bottoms/jeans', 1),
      link('boys-shorts', 'Shorts', '/boys/bottoms/shorts', 2),
    ]),
    link('boys-casual', 'Casual shirts', '/boys/casual-shirts', 3),
  ]),
};

const pageSlugs: readonly [string, string][] = [
  ['delivery', 'Delivery'],
  ['exchanges', 'Exchanges'],
  ['size-guide', 'Size guide'],
  ['our-story', 'Our story'],
  ['made-in-pakistan', 'Made in Pakistan'],
  ['contact', 'Contact'],
  ['privacy', 'Privacy'],
  ['terms', 'Terms'],
];
const pages: readonly PageSummary[] = pageSlugs.map(([slug, title]) => ({ slug, title, status: 'published' }));
const page = (label: string, slug: string) => ({ label, target: { kind: 'page', slug } as const, enabled: true });
const path = (label: string, p: string) => ({ label, target: { kind: 'path', path: p } as const, enabled: true });

export const launchSettings: SiteSettings = {
  version: 0,
  brand: { name: 'Romp', tagline: 'Children’s clothing', whatsapp: '923315613217' },
  announcement: { text: '', enabled: false },
  navigation: [
    girls,
    boys,
    link('new-in', 'New in', '/new-in', 3),
    { key: 'collections', label: 'Collections', kind: 'collection-list', target: { kind: 'path', path: '/collections' }, enabled: true, sort: 4, children: [] },
    link('sale', 'Sale', '/sale', 5),
  ],
  ages: Array.from({ length: 8 }, (_, i) => ({ code: `${i + 1}y`, label: `${i + 1}Y`, sort: i + 1, enabled: true })),
  footer: [
    { title: 'Shop', sort: 1, links: [path('New in', '/new-in'), path('Girls', '/girls'), path('Boys', '/boys'), path('Collections', '/collections'), path('Sale', '/sale')] },
    {
      title: 'Help', sort: 2,
      links: [page('Delivery', 'delivery'), page('Exchanges', 'exchanges'), page('Size guide', 'size-guide'), page('Contact', 'contact'),
        { label: 'WhatsApp', target: { kind: 'external', url: 'https://wa.me/923315613217' }, enabled: true }],
    },
    { title: 'Romp', sort: 3, links: [page('Our story', 'our-story'), page('Made in Pakistan', 'made-in-pakistan'), page('Privacy', 'privacy'), page('Terms', 'terms')] },
  ],
  delivery: [],
  exchange: { windowDays: 0, conditions: [] },
  pages,
  popularSearches: ['Dresses', 'T-shirts', 'Shorts'],
};
