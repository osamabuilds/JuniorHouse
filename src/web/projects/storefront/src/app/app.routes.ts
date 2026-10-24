import { isDevMode } from '@angular/core';
import { Routes } from '@angular/router';

/** The foundations page is a visual check for the design system; it is not part of production builds. Until the home page exists (Story 3), `/` redirects to it in development only. */
const devRoutes: Routes = isDevMode()
  ? [
      { path: '', pathMatch: 'full', redirectTo: '_foundations' },
      {
        path: '_foundations',
        loadComponent: () =>
          import('./foundations/foundations.page').then((m) => m.FoundationsPage),
      },
    ]
  : [];

/**
 * Content pages are served from the settings by slug. The catch-all gives every other address a real 404 until the
 * home page, listings and product pages (stories 3 to 5) take their routes ahead of it.
 */
export const routes: Routes = [
  ...devRoutes,
  { path: ':slug', loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
  { path: '**', data: { notFound: true }, loadComponent: () => import('./features/storefront/page/containers/page/page.container').then((m) => m.PageContainer) },
];
