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

export const routes: Routes = [...devRoutes];
