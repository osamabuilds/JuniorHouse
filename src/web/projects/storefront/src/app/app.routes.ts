import { isDevMode } from '@angular/core';
import { Routes, UrlMatcher, UrlSegment } from '@angular/router';
import { audienceCanMatch } from './features/storefront/listings/listings.guards';
import { CATALOG } from './shared/data/mock-catalog';

/** The foundations page is a visual check for the design system; it is not part of production builds. */
const devRoutes: Routes = isDevMode()
  ? [
      {
        path: '_foundations',
        loadComponent: () =>
          import('./foundations/foundations.page').then((m) => m.FoundationsPage),
      },
    ]
  : [];

/** Only a known product reaches the product page; any other three-part address falls through to the real 404. Product pages still run on sample data until S9b. */
const productMatcher: UrlMatcher = (segments: UrlSegment[]) =>
  segments.length === 3 && CATALOG.some((p) => p.slug === segments[2].path)
    ? { consumed: segments, posParams: { category: segments[0], type: segments[1], slug: segments[2] } }
    : null;

const listing = () => import('./features/storefront/listings/containers/listing-page/listing-page.container').then((m) => m.ListingPageContainer);

/**
 * Listings run on the read model through the API (listings LST-*). Content pages are served from the settings by slug; the catch-all
 * gives every other address a real 404.
 */
export const routes: Routes = [
  ...devRoutes,
  {
    path: '',
    pathMatch: 'full',
    title: 'Romp Kids | Considered western wear for children',
    loadComponent: () =>
      import('./features/catalog/home/home-page.container').then((m) => m.HomePageContainer),
  },
  { path: 'new-in', data: { kind: 'new-in' }, loadComponent: listing },
  { path: 'sale', data: { kind: 'sale' }, loadComponent: listing },
  { path: 'collections/:code', data: { kind: 'collection' }, loadComponent: listing },
  {
    matcher: productMatcher,
    title: 'Product | Romp Kids',
    loadComponent: () =>
      import('./features/catalog/product/product-page.container').then((m) => m.ProductPageContainer),
  },
  { path: ':gender/new-in', data: { kind: 'new-in' }, loadComponent: listing },
  { path: ':gender/sale', data: { kind: 'sale' }, loadComponent: listing },
  { path: ':gender/:category', data: { kind: 'category' }, loadComponent: listing },
  { path: ':gender', canMatch: [audienceCanMatch], data: { kind: 'audience' }, loadComponent: listing },
  { path: ':slug', loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
  { path: '**', data: { notFound: true }, loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
];
