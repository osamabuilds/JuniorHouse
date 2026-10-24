import { ContentPage, DraftSettings, NavItem, PageBlock, PageRow, SnapshotRow } from '@features/storefront/storefront-settings/models';

export function navItem(overrides: Partial<NavItem> = {}): NavItem {
  return {
    key: 'girls',
    label: 'Girls',
    kind: 'Link',
    target: { kind: 'Path', reference: '/girls' },
    sort: 1,
    enabled: true,
    showAges: false,
    children: [],
    ...overrides,
  };
}

export function pageRow(overrides: Partial<PageRow> = {}): PageRow {
  return { id: 1, slug: 'delivery', title: 'Delivery', status: 'Draft', blockCount: 3, ...overrides };
}

export function draftSettings(overrides: Partial<DraftSettings> = {}): DraftSettings {
  return {
    version: 4,
    navigation: [
      navItem({ children: [navItem({ key: 'dresses', label: 'Dresses', target: { kind: 'Path', reference: '/girls/dresses' } })] }),
      navItem({ key: 'sale', label: 'Sale', sort: 2, target: { kind: 'Path', reference: '/sale' } }),
    ],
    ages: [
      { code: '1y', label: '1Y', sort: 1, enabled: true },
      { code: '2y', label: '2Y', sort: 2, enabled: true },
    ],
    footer: [{ title: 'Help', sort: 1, links: [{ label: 'Delivery', target: { kind: 'Page', reference: 'delivery' }, sort: 1, enabled: true }] }],
    items: [
      { key: 'BrandName', value: 'Romp' },
      { key: 'Whatsapp', value: '+923315613217' },
    ],
    popularSearches: ['Dresses', 'Shorts'],
    delivery: [{ city: 'Lahore', minDays: 2, maxDays: 3, charge: 250, freeOver: 5000, courier: 'TCS', enabled: true }],
    exchange: { windowDays: 7, conditions: ['Unworn.'] },
    pages: [pageRow()],
    ...overrides,
  };
}

export function block(overrides: Partial<PageBlock> = {}): PageBlock {
  return { type: 'Paragraph', text: 'Hello.', level: 0, factKey: null, label: null, ordered: false, items: [], ...overrides };
}

export function contentPage(overrides: Partial<ContentPage> = {}): ContentPage {
  return {
    id: 1,
    slug: 'delivery',
    title: 'Delivery',
    metaDescription: 'How we deliver.',
    status: 'Draft',
    rowVersion: 9,
    blocks: [block({ type: 'Heading', text: 'Delivery', level: 2 }), block()],
    ...overrides,
  };
}

export function snapshotRow(overrides: Partial<SnapshotRow> = {}): SnapshotRow {
  return {
    versionNo: 3,
    documentHash: 'a'.repeat(64),
    note: 'March launch',
    publishedAt: '2026-10-24T09:00:00Z',
    publishedBy: 'system',
    isCurrent: true,
    ...overrides,
  };
}
