import { isDevMode } from '@angular/core';
import { Routes, UrlMatcher, UrlSegment } from '@angular/router';
import { CATALOG, CATEGORY_NAMES } from './shared/data/mock-catalog';

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

/** Matches only the known category slugs, so content pages (`/delivery`, `/size-guide`) still reach the page route. */
const categoryMatcher: UrlMatcher = (segments: UrlSegment[]) =>
  segments.length === 1 && segments[0].path in CATEGORY_NAMES
    ? { consumed: segments, posParams: { category: segments[0] } }
    : null;

/** Only a known product reaches the product page; any other three-part address falls through to the real 404. */
const productMatcher: UrlMatcher = (segments: UrlSegment[]) =>
  segments.length === 3 && CATALOG.some((p) => p.slug === segments[2].path)
    ? { consumed: segments, posParams: { category: segments[0], type: segments[1], slug: segments[2] } }
    : null;

/**
 * Catalogue pages run on static sample data until the catalogue API lands. Content pages are served from the
 * settings by slug; the catch-all gives every other address a real 404.
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
  {
    matcher: categoryMatcher,
    title: 'Shop | Romp Kids',
    loadComponent: () =>
      import('./features/catalog/listing/listing-page.container').then((m) => m.ListingPageContainer),
  },
  {
    matcher: productMatcher,
    title: 'Product | Romp Kids',
    loadComponent: () =>
      import('./features/catalog/product/product-page.container').then((m) => m.ProductPageContainer),
  },
  { path: ':slug', loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
  { path: '**', data: { notFound: true }, loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
];
