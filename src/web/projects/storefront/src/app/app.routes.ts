import { isDevMode } from '@angular/core';
import { Routes, UrlMatcher, UrlSegment } from '@angular/router';
import { audienceCanMatch } from './features/storefront/listings/listings.guards';

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

/**
 * A product address has three parts (/{audience}/{category}/{slug}). Whether it exists is the API answer, which becomes a real 301, 404 or
 * 410 on the product page itself (PDP-13); no other route has three parts.
 */
export const productMatcher: UrlMatcher = (segments: UrlSegment[]) => (segments.length === 3 ? { consumed: segments } : null);

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
    loadComponent: () =>
      import('./features/storefront/product-pages/containers/product-page/product-page.container').then((m) => m.ProductPageContainer),
  },
  { path: ':gender/new-in', data: { kind: 'new-in' }, loadComponent: listing },
  { path: ':gender/sale', data: { kind: 'sale' }, loadComponent: listing },
  { path: ':gender/:category', data: { kind: 'category' }, loadComponent: listing },
  { path: ':gender', canMatch: [audienceCanMatch], data: { kind: 'audience' }, loadComponent: listing },
  { path: ':slug', loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
  { path: '**', data: { notFound: true }, loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
];
