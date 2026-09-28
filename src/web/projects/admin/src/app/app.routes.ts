import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'reference-data' },
  {
    path: 'reference-data',
    loadComponent: () =>
      import('./reference-data/reference-data-page').then((m) => m.ReferenceDataPage),
    title: 'Reference Data · Romp Admin',
  },
  {
    path: 'styles',
    loadComponent: () => import('./styles/styles-page').then((m) => m.StylesPage),
    title: 'Styles · Romp Admin',
  },
  {
    path: 'vendors',
    loadComponent: () => import('./vendors/vendors-page').then((m) => m.VendorsPage),
    title: 'Vendors · Romp Admin',
  },
  {
    path: 'purchase-orders',
    loadComponent: () =>
      import('./purchase-orders/purchase-orders-page').then((m) => m.PurchaseOrdersPage),
    title: 'Purchase Orders · Romp Admin',
  },
];
