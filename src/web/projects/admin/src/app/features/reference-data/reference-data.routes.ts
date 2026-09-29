import { Routes } from '@angular/router';

export const REFERENCE_DATA_ROUTES: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./containers/reference-data-page/reference-data-page.container').then((m) => m.ReferenceDataPageContainer),
    title: 'Reference Data · Romp Admin',
  },
];
