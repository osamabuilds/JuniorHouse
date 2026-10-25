import { SectionLink } from '@shared';
import { BlockType, LinkTargetKind, NavKind, PageStatus } from './models';

/** The Storefront settings screens, shown as the section navigation on each of them. */
export const STOREFRONT_LINKS: readonly SectionLink[] = [
  { label: 'Navigation', path: '/storefront/navigation' },
  { label: 'Ages', path: '/storefront/ages' },
  { label: 'Footer', path: '/storefront/footer' },
  { label: 'Settings', path: '/storefront/contact' },
  { label: 'Delivery and exchange', path: '/storefront/delivery' },
  { label: 'Pages', path: '/storefront/pages' },
  { label: 'Search', path: '/storefront/search' },
  { label: 'Publish', path: '/storefront/publish' },
];

export interface Option<T extends string> {
  readonly value: T;
  readonly label: string;
}

export const NAV_KIND_OPTIONS: readonly Option<NavKind>[] = [
  { value: 'Link', label: 'Link (opens a page, may also have members)' },
  { value: 'Group', label: 'Group (only opens its members)' },
  { value: 'CollectionList', label: 'Collections (lists the collections)' },
];

/** Category links are left out until the catalogue categories exist (SCRUM-207); the server refuses them too. */
export const TARGET_KIND_OPTIONS: readonly Option<LinkTargetKind>[] = [
  { value: 'Path', label: 'A path on the shop' },
  { value: 'Page', label: 'A content page' },
  { value: 'Collection', label: 'A collection' },
  { value: 'External', label: 'Another website' },
];

export const TARGET_REFERENCE_HINTS: Readonly<Record<LinkTargetKind, string>> = {
  Path: 'For example /girls/dresses',
  Page: 'The page address, for example size-guide',
  Collection: 'The collection code, for example summer-edit',
  Category: 'The category code',
  External: 'A full https:// address on the allowed list',
};

export const PAGE_STATUS_OPTIONS: readonly Option<PageStatus>[] = [
  { value: 'Draft', label: 'Draft' },
  { value: 'Published', label: 'Published' },
  { value: 'Archived', label: 'Archived' },
];

export const BLOCK_TYPE_OPTIONS: readonly Option<BlockType>[] = [
  { value: 'Heading', label: 'Heading' },
  { value: 'Paragraph', label: 'Paragraph' },
  { value: 'List', label: 'List' },
  { value: 'Table', label: 'Table' },
  { value: 'Callout', label: 'Callout' },
  { value: 'Fact', label: 'A value from the settings' },
];

/** The values a page may quote. A page that quotes one still empty cannot be published (ST-17). */
export const FACT_OPTIONS: readonly Option<string>[] = [
  { value: 'contact.whatsapp', label: 'WhatsApp number' },
  { value: 'contact.email', label: 'Email address' },
  { value: 'contact.hours', label: 'Opening hours' },
  { value: 'exchange.window', label: 'Exchange window' },
  { value: 'exchange.conditions', label: 'Exchange conditions' },
  { value: 'delivery.rules', label: 'Delivery table' },
];

export const MAX_BLOCKS_PER_PAGE = 60;
export const MAX_PARAGRAPH_LENGTH = 5000;
export const MAX_META_DESCRIPTION_LENGTH = 160;
export const MAX_POPULAR_SEARCHES = 12;
