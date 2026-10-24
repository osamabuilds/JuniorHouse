/** Where a navigation or footer link goes. A template never builds a URL: `resolveHref` does. */
export type LinkTarget =
  | { readonly kind: 'page'; readonly slug: string }
  | { readonly kind: 'path'; readonly path: string }
  | { readonly kind: 'category'; readonly code: string; readonly path: string }
  | { readonly kind: 'collection'; readonly code: string }
  | { readonly kind: 'external'; readonly url: string };

export type NavKind = 'link' | 'group' | 'collection-list';

/** One navigation item. A tree: main category, its members, and their further members (three levels in all). */
export interface NavItem {
  readonly key: string;
  readonly label: string;
  readonly kind: NavKind;
  readonly target?: LinkTarget;
  readonly enabled: boolean;
  readonly sort: number;
  readonly children: readonly NavItem[];
  /** The mega-menu adds a "Shop by age" column of the configured ages (links carry the age code). */
  readonly showAges?: boolean;
}

export interface AgeStep {
  readonly code: string;
  readonly label: string;
  readonly sort: number;
  readonly enabled: boolean;
}

export interface FooterLink {
  readonly label: string;
  readonly target: LinkTarget;
  readonly enabled: boolean;
}

export interface FooterColumn {
  readonly title: string;
  readonly sort: number;
  readonly links: readonly FooterLink[];
}

export interface BrandSettings {
  readonly name: string;
  readonly tagline: string;
  /** International format without separators, for example 923315613217. */
  readonly whatsapp: string;
  readonly email?: string;
  readonly hours?: string;
}

export interface DeliveryRule {
  readonly city: string;
  readonly minDays: number;
  readonly maxDays: number;
  /** PKR, two decimals. */
  readonly charge: number;
  readonly freeOver?: number;
  readonly courier: string;
  readonly enabled: boolean;
}

export type PageStatus = 'draft' | 'published' | 'archived';

export interface PageSummary {
  readonly slug: string;
  readonly title: string;
  readonly status: PageStatus;
}

/** The public snapshot of the admin-managed storefront settings (SCRUM-206 produces it; SCRUM-205 renders it). */
export interface SiteSettings {
  readonly version: number;
  readonly brand: BrandSettings;
  readonly announcement: { readonly text: string; readonly enabled: boolean };
  readonly navigation: readonly NavItem[];
  readonly ages: readonly AgeStep[];
  readonly footer: readonly FooterColumn[];
  readonly delivery: readonly DeliveryRule[];
  readonly exchange: { readonly windowDays: number; readonly conditions: readonly string[] };
  readonly pages: readonly PageSummary[];
}

export type PageBlock =
  | { readonly type: 'heading'; readonly level: 2 | 3; readonly text: string }
  | { readonly type: 'paragraph'; readonly text: string }
  | { readonly type: 'list'; readonly ordered: boolean; readonly items: readonly string[] }
  | { readonly type: 'table'; readonly caption?: string; readonly headers: readonly string[]; readonly rows: readonly (readonly string[])[] }
  | { readonly type: 'callout'; readonly text: string }
  | { readonly type: 'fact'; readonly key: string };

export interface ContentPage {
  readonly slug: string;
  readonly title: string;
  readonly description: string;
  readonly status: PageStatus;
  readonly blocks: readonly PageBlock[];
}

/** Deepest nesting allowed in the navigation tree (main category, member, further member). One constant, so it can be raised. */
export const MAX_NAV_DEPTH = 3;
