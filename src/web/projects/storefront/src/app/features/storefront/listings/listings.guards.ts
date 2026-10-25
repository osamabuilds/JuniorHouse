import { inject } from '@angular/core';
import { CanMatchFn } from '@angular/router';
import { Store } from '@ngrx/store';
import { filter, map, take } from 'rxjs';
import { siteSettingsFeature } from '../site-settings/store/site-settings.reducer';

/**
 * A single address segment is an audience listing unless it is a published content page (delivery, size-guide, ...): pages win, so the
 * two never clash (the reserved slugs in the admin app also keep them apart). Whether the audience exists is the API's answer, which
 * becomes a real 404 (LST-11).
 */
/** Single segments that are never an audience: they belong to features that are not listings. */
const RESERVED = new Set(['search', 'cart', 'checkout', 'account', 'wishlist']);

export const audienceCanMatch: CanMatchFn = (_route, segments) => {
  const store = inject(Store);
  const slug = segments[0]?.path ?? '';
  if (RESERVED.has(slug)) return false;
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(slug)) return false;
  return store.select(siteSettingsFeature.selectStatus).pipe(
    filter((status) => status === 'loaded' || status === 'failed'),
    take(1),
    map(() => true),
    // The settings are read straight from the store after they have loaded.
    map(() => !publishedPages(store).has(slug)),
  );
};

function publishedPages(store: Store): Set<string> {
  let pages = new Set<string>();
  store.select(siteSettingsFeature.selectSettings).pipe(take(1)).subscribe((s) => (pages = new Set(s.pages.filter((p) => p.status === 'published').map((p) => p.slug))));
  return pages;
}
