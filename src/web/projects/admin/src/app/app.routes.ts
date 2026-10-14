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
    path: 'catalog',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'products' },
      {
        path: 'products',
        loadChildren: () => import('@features/catalog/products/products.routes').then((m) => m.PRODUCTS_ROUTES),
      },
      {
        path: 'size-charts',
        loadChildren: () => import('@features/catalog/size-charts/size-charts.routes').then((m) => m.SIZE_CHARTS_ROUTES),
      },
    ],
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
    path: 'labelling',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'worklist' },
      {
        path: 'worklist',
        loadChildren: () => import('@features/labelling/label-work/label-work.routes').then((m) => m.LABEL_WORK_ROUTES),
      },
      {
        path: 'queue',
        loadChildren: () => import('@features/labelling/print-jobs/print-jobs.routes').then((m) => m.PRINT_JOBS_ROUTES),
      },
      {
        path: 'register',
        loadChildren: () => import('@features/labelling/variants/variants.routes').then((m) => m.VARIANTS_ROUTES),
      },
    ],
  },
  {
    path: 'warehouse',
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'putaway' },
      {
        path: 'bins',
        loadChildren: () => import('@features/warehouse/bins/bins.routes').then((m) => m.BINS_ROUTES),
      },
      {
        path: 'putaway',
        loadChildren: () => import('@features/warehouse/putaway/putaway.routes').then((m) => m.PUTAWAY_ROUTES),
      },
      {
        path: 'adjustments',
        loadChildren: () => import('@features/warehouse/adjustments/adjustments.routes').then((m) => m.ADJUSTMENTS_ROUTES),
      },
      {
        path: 'cycle-counts',
        loadChildren: () => import('@features/warehouse/cycle-counts/cycle-counts.routes').then((m) => m.CYCLE_COUNTS_ROUTES),
      },
      {
        path: 'alerts',
        loadChildren: () => import('@features/warehouse/stock-alerts/stock-alerts.routes').then((m) => m.STOCK_ALERTS_ROUTES),
      },
      {
        path: 'stock',
        loadChildren: () => import('@features/warehouse/stock-ledger/stock-ledger.routes').then((m) => m.STOCK_LEDGER_ROUTES),
      },
    ],
  },
  {
    path: 'purchase-orders',
    loadChildren: () => import('@features/vendor/purchase-orders/purchase-orders.routes').then((m) => m.PURCHASE_ORDERS_ROUTES),
  },
];
