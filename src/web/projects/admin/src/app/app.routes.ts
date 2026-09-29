import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'reference-data' },
  {
    path: 'reference-data',
    loadChildren: () => import('@features/reference-data/reference-data.routes').then((m) => m.REFERENCE_DATA_ROUTES),
  },
  {
    path: 'styles',
    loadChildren: () => import('@features/styles/styles.routes').then((m) => m.STYLES_ROUTES),
  },
  {
    path: 'vendors',
    loadChildren: () => import('@features/vendors/vendors.routes').then((m) => m.VENDORS_ROUTES),
  },
  {
    // SCRUM-93 task 51: what a vendor sees of a PO. The shell hides its navigation on this route.
    path: 'purchase-orders/:id/vendor-view',
    loadComponent: () =>
      import('./purchase-orders/vendor-view-page').then((m) => m.VendorViewPage),
    title: 'Purchase Order · Romp',
  },
  {
    path: 'purchase-orders',
    loadComponent: () =>
      import('./purchase-orders/purchase-orders-page').then((m) => m.PurchaseOrdersPage),
    title: 'Purchase Orders · Romp Admin',
  },
];
