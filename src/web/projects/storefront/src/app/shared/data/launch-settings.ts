import { NavItem, PageSummary, SiteSettings } from '../models/site-settings.model';

/**
 * The built-in launch set: the same shape the settings API returns (SCRUM-206), used until that API exists and as the
 * last resort when it is unreachable. Placeholder category paths stay valid until Catalog has its category tree.
 * It holds no business facts (delivery, exchange): those are entered in the admin app.
 */
const link = (key: string, label: string, path: string, sort: number, children: readonly NavItem[] = []): NavItem => ({
  key, label, kind: children.length > 0 ? 'group' : 'link', target: { kind: 'path', path }, enabled: true, sort, children,
});

/** The categories of the first range, with our own labels; each is a category page of an audience and of a season (sold-out pieces included). */
const categories: readonly (readonly [string, string])[] = [
  ['t-shirts', 'T-shirts'],
  ['sweaters', 'Sweaters'],
  ['jackets', 'Jackets'],
  ['trousers', 'Trousers'],
  ['shorts', 'Shorts'],
  ['dresses', 'Dresses'],
];

const under = (key: string, label: string, path: string, sort: number, skip: readonly string[] = []): NavItem =>
  link(key, label, path, sort, categories.filter(([slug]) => !skip.includes(slug)).map(([slug, name], i) => link(`${key}-${slug}`, name, `${path}/${slug}`, i + 1)));

const winter: NavItem = under('winter', 'Winter', '/collections/winter', 1);
const summer: NavItem = under('summer', 'Summer', '/collections/summer', 2);
const girls: NavItem = { showAges: true, ...under('girls', 'Girls', '/girls', 3) };
const boys: NavItem = { showAges: true, ...under('boys', 'Boys', '/boys', 4, ['dresses']) };

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
/** Privacy and terms are legal text the owner supplies: they stay drafts (not linked, not served) until published in the admin app. */
const drafts = new Set(['privacy', 'terms']);
const pages: readonly PageSummary[] = pageSlugs.map(([slug, title]) => ({ slug, title, status: drafts.has(slug) ? 'draft' : 'published' }));
const page = (label: string, slug: string) => ({ label, target: { kind: 'page', slug } as const, enabled: true });
const path = (label: string, p: string) => ({ label, target: { kind: 'path', path: p } as const, enabled: true });

export const launchSettings: SiteSettings = {
  version: 0,
  brand: { name: 'Romp', tagline: 'Children’s clothing', whatsapp: '923315613217' },
  announcement: { text: '', enabled: false },
  navigation: [
    winter,
    summer,
    girls,
    boys,
    link('new-in', 'New in', '/new-in', 5),
    link('sale', 'Sale', '/sale', 6),
  ],
  ages: Array.from({ length: 8 }, (_, i) => ({ code: `${i + 1}y`, label: `${i + 1}Y`, sort: i + 1, enabled: true })),
  footer: [
    { title: 'Shop', sort: 1, links: [path('New in', '/new-in'), path('Girls', '/girls'), path('Boys', '/boys'), path('Collections', '/collections'), path('Sale', '/sale')] },
    {
      title: 'Help', sort: 2,
      links: [page('Delivery', 'delivery'), page('Exchanges', 'exchanges'), page('Size guide', 'size-guide'), page('Contact', 'contact'), path('Track order', '/track-order'),
        { label: 'WhatsApp', target: { kind: 'external', url: 'https://wa.me/923315613217' }, enabled: true }],
    },
    { title: 'Romp', sort: 3, links: [page('Our story', 'our-story'), page('Made in Pakistan', 'made-in-pakistan'), page('Privacy', 'privacy'), page('Terms', 'terms')] },
  ],
  delivery: [],
  exchange: { windowDays: 0, conditions: [] },
  pages,
  popularSearches: ['Dresses', 'T-shirts', 'Shorts'],
};
