import { createSelector } from '@ngrx/store';
import { AgeStep, DeliveryRule } from '../../../../shared/models/site-settings.model';
import { whatsappLink } from '../../../../shared/utils/whatsapp';
import { resolveHref } from '../../../../shared/utils/resolve-href';
import { visibleEntries } from '../../../../shared/utils/visible-entries';
import { siteSettingsFeature } from './site-settings.reducer';

export interface FooterLinkView { readonly label: string; readonly href: string }
export interface FooterColumnView { readonly title: string; readonly links: readonly FooterLinkView[] }

const { selectSettings } = siteSettingsFeature;
export { selectSettings };

export const selectHeaderEntries = createSelector(selectSettings, (s) => visibleEntries(s.navigation));
export const selectBrand = createSelector(selectSettings, (s) => s.brand);
export const selectAnnouncement = createSelector(selectSettings, (s) => (s.announcement.enabled && s.announcement.text.trim() ? s.announcement.text : null));
export const selectAges = createSelector(selectSettings, (s): readonly AgeStep[] => [...s.ages].filter((a) => a.enabled).sort((a, b) => a.sort - b.sort));

/** Footer columns with resolved hrefs. A link to a page shows only while that page is published; unsafe links are dropped. */
export const selectFooter = createSelector(selectSettings, (s): readonly FooterColumnView[] => {
  const published = new Set(s.pages.filter((p) => p.status === 'published').map((p) => p.slug));
  return [...s.footer]
    .sort((a, b) => a.sort - b.sort)
    .map((col) => ({
      title: col.title,
      links: col.links
        .filter((l) => l.enabled && (l.target.kind !== 'page' || published.has(l.target.slug)))
        .flatMap((l) => {
          const href = resolveHref(l.target);
          return href ? [{ label: l.label, href }] : [];
        }),
    }))
    .filter((col) => col.links.length > 0);
});

export const selectPopularSearches = createSelector(selectSettings, (s) => s.popularSearches);

export interface FactsView {
  readonly whatsapp: { readonly href: string; readonly display: string } | null;
  readonly email: string | null;
  readonly hours: string | null;
  readonly exchangeWindowDays: number | null;
  readonly exchangeConditions: readonly string[];
  readonly deliveryRules: readonly DeliveryRule[];
}

/** Business facts entered in the admin app. A missing fact is null or empty, and the page shows nothing for it. */
export const selectFacts = createSelector(selectSettings, (s): FactsView => ({
  whatsapp: whatsappLink(s.brand.whatsapp),
  email: s.brand.email?.trim() || null,
  hours: s.brand.hours?.trim() || null,
  exchangeWindowDays: s.exchange.windowDays > 0 ? s.exchange.windowDays : null,
  exchangeConditions: s.exchange.conditions,
  deliveryRules: s.delivery.filter((d) => d.enabled),
}));

/** Top-level entries as plain links (for the not-found page and search empty states). Unsafe targets are dropped. */
export const selectTopLinks = createSelector(selectHeaderEntries, (entries) =>
  entries.flatMap((e) => {
    const href = e.target ? resolveHref(e.target) : null;
    return href ? [{ label: e.label, href }] : [];
  }),
);

/** The help menu on a content page: every published page, in the order the settings list them. */
export const selectHelpPages = createSelector(selectSettings, (s) =>
  s.pages.filter((p) => p.status === 'published').map((p) => ({ label: p.title, href: `/${p.slug}` })),
);
