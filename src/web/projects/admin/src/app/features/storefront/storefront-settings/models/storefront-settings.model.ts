/** Shapes of the storefront settings API (docs/specs/storefront/storefront-settings/plan.md). Enums travel by name. */

export type NavKind = 'Link' | 'Group' | 'CollectionList';
export type LinkTargetKind = 'Path' | 'Page' | 'Collection' | 'Category' | 'External';
export type PageStatus = 'Draft' | 'Published' | 'Archived';
export type BlockType = 'Heading' | 'Paragraph' | 'List' | 'Table' | 'Callout' | 'Fact';
export type SettingKey = 'BrandName' | 'Tagline' | 'Whatsapp' | 'Email' | 'Hours' | 'Announcement' | 'AnnouncementOn';

export interface Target {
  readonly kind: LinkTargetKind;
  readonly reference: string;
}

/** One entry of the navigation tree. Up to three levels (the server enforces it; the editor warns first). */
export interface NavItem {
  readonly key: string;
  readonly label: string;
  readonly kind: NavKind;
  readonly target: Target | null;
  readonly sort: number;
  readonly enabled: boolean;
  readonly showAges: boolean;
  readonly children: readonly NavItem[];
}

export interface AgeStep {
  readonly code: string;
  readonly label: string;
  readonly sort: number;
  readonly enabled: boolean;
}

export interface FooterLink {
  readonly label: string;
  readonly target: Target;
  readonly sort: number;
  readonly enabled: boolean;
}

export interface FooterColumn {
  readonly title: string;
  readonly sort: number;
  readonly links: readonly FooterLink[];
}

export interface SettingItem {
  readonly key: SettingKey;
  readonly value: string;
}

export interface DeliveryRule {
  readonly city: string;
  readonly minDays: number;
  readonly maxDays: number;
  /** PKR. */
  readonly charge: number;
  readonly freeOver: number | null;
  readonly courier: string;
  readonly enabled: boolean;
}

export interface Exchange {
  readonly windowDays: number;
  readonly conditions: readonly string[];
}

export interface PageBlockItem {
  readonly row: number;
  readonly column: number;
  readonly text: string;
}

export interface PageBlock {
  readonly type: BlockType;
  readonly text: string;
  readonly level: number;
  readonly factKey: string | null;
  readonly label: string | null;
  readonly ordered: boolean;
  readonly items: readonly PageBlockItem[];
}

export interface PageRow {
  readonly id: number;
  readonly slug: string;
  readonly title: string;
  readonly status: PageStatus;
  readonly blockCount: number;
}

export interface ContentPage {
  readonly id: number;
  readonly slug: string;
  readonly title: string;
  readonly metaDescription: string;
  readonly status: PageStatus;
  /** The row version to quote back on the next save (a stale one is refused, ST-14). */
  readonly rowVersion: number;
  readonly blocks: readonly PageBlock[];
}

/** The working copy of everything except page bodies. `version` is quoted on every save (ST-14). */
export interface DraftSettings {
  readonly version: number;
  readonly navigation: readonly NavItem[];
  readonly ages: readonly AgeStep[];
  readonly footer: readonly FooterColumn[];
  readonly items: readonly SettingItem[];
  readonly popularSearches: readonly string[];
  readonly delivery: readonly DeliveryRule[];
  readonly exchange: Exchange | null;
  readonly pages: readonly PageRow[];
}

export interface Saved<T> {
  readonly version: number;
  readonly data: T;
}

export interface ContactSettings {
  readonly items: readonly SettingItem[];
  readonly popularSearches: readonly string[];
}

export interface DeliveryAndExchange {
  readonly rules: readonly DeliveryRule[];
  readonly exchange: Exchange | null;
}

export interface SnapshotRow {
  readonly versionNo: number;
  readonly documentHash: string;
  readonly note: string;
  readonly publishedAt: string;
  readonly publishedBy: string;
  readonly isCurrent: boolean;
}

export interface PublishResult {
  readonly versionNo: number;
  readonly documentHash: string;
  readonly created: boolean;
}

/** What the page editor sends; the id is in the route and the row version comes from the loaded page. */
export interface PageValue {
  readonly slug: string;
  readonly title: string;
  readonly metaDescription: string;
  readonly blocks: readonly PageBlock[];
}

export interface PageFilters {
  readonly status: PageStatus | '';
  readonly page: number;
  readonly pageSize: number;
}

/** The parts of the draft that have their own Save button. */
export type DraftPart =
  | { readonly part: 'navigation'; readonly items: readonly NavItem[] }
  | { readonly part: 'ages'; readonly steps: readonly AgeStep[] }
  | { readonly part: 'footer'; readonly columns: readonly FooterColumn[] }
  | { readonly part: 'contact'; readonly items: readonly SettingItem[]; readonly popularSearches: readonly string[] }
  | { readonly part: 'delivery'; readonly rules: readonly DeliveryRule[]; readonly exchange: Exchange | null };

/** What a successful save changes in the stored draft: the new version plus the part as saved. */
export type DraftPatch = Partial<Omit<DraftSettings, 'version' | 'pages'>>;
