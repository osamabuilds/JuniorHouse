import { isDevMode } from '@angular/core';
import { Routes, UrlMatcher, UrlSegment } from '@angular/router';
import { CATEGORY_NAMES } from './features/catalog/catalog.sample-data';

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
      import('./features/catalog/home/containers/home-page/home-page.container').then((m) => m.HomePageContainer),
  },
  {
    matcher: categoryMatcher,
    title: 'Shop | Romp Kids',
    loadComponent: () =>
      import('./features/catalog/category/containers/category-page/category-page.container').then((m) => m.CategoryPageContainer),
  },
  {
    path: ':category/:type/:slug',
    title: 'Product | Romp Kids',
    loadComponent: () =>
      import('./features/catalog/product-detail/containers/product-page/product-page.container').then((m) => m.ProductPageContainer),
  },
  { path: ':slug', loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
  { path: '**', data: { notFound: true }, loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
];
