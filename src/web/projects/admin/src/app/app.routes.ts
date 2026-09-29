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
    path: 'purchase-orders',
    loadChildren: () => import('@features/purchase-orders/purchase-orders.routes').then((m) => m.PURCHASE_ORDERS_ROUTES),
  },
];
