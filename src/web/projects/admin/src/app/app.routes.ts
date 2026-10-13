import { Routes } from '@angular/router';

export const routes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'reference-data' },
  {
    path: 'reference-data',
    loadChildren: () => import('@features/reference/lookups/lookups.routes').then((m) => m.LOOKUPS_ROUTES),
  },
  {
    path: 'styles',
    loadChildren: () => import('@features/catalog/styles/styles.routes').then((m) => m.STYLES_ROUTES),
  },
  {
    path: 'vendors',
    loadChildren: () => import('@features/vendor/vendors/vendors.routes').then((m) => m.VENDORS_ROUTES),
  },
  {
    path: 'production',
    loadChildren: () => import('@features/production/production-runs/production-runs.routes').then((m) => m.PRODUCTION_RUNS_ROUTES),
  },
  {
    path: 'quality',
    loadChildren: () => import('@features/quality/batches/batches.routes').then((m) => m.BATCHES_ROUTES),
  },
  {
    path: 'purchase-orders',
    loadChildren: () => import('@features/vendor/purchase-orders/purchase-orders.routes').then((m) => m.PURCHASE_ORDERS_ROUTES),
  },
];
