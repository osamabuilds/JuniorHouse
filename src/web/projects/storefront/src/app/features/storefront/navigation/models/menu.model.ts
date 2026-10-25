export type MenuEntryKind = 'new-in' | 'sale' | 'category' | 'collection' | 'age';

/** One link of the header menu, as GET /api/storefront/menu returns it. The path may carry a query (/girls?age=age-2y). */
export interface MenuEntry {
  readonly kind: MenuEntryKind;
  readonly code: string;
  readonly label: string;
  readonly path: string;
}

export interface MenuAudience {
  readonly code: string;
  readonly name: string;
  readonly path: string;
  readonly entries: readonly MenuEntry[];
}

/** The whole menu, built by the server from lookup rows and the published catalogue (navigation NAV-1..9). */
export interface Menu {
  readonly audiences: readonly MenuAudience[];
  readonly collections: readonly MenuEntry[];
  readonly hasNewIn: boolean;
  readonly hasSale: boolean;
}
