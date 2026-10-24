import { createSelector } from '@ngrx/store';
import { AgeStep } from '../../../../shared/models/site-settings.model';
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
