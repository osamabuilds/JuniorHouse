import { Routes } from '@angular/router';
import { authGuard, permissionGuard } from '@core/auth';
import { MFA_SECURITY_ROUTES, MFA_SETUP_ROUTES } from '@features/identity/mfa';
import { ROLES_ROUTES } from '@features/identity/roles';
import { SESSIONS_ROUTES } from '@features/identity/sessions';

const staffRoutes: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'reference-data' },
  {
    path: 'reference-data',
    canActivate: [permissionGuard('ref.read')],
    loadChildren: () => import('@features/reference/lookups/lookups.routes').then((m) => m.LOOKUPS_ROUTES),
  },
  {
    path: 'staff',
    canActivate: [permissionGuard('staff.manage')],
    loadChildren: () => import('@features/identity/staff-accounts/staff-accounts.routes').then((m) => m.STAFF_ACCOUNTS_ROUTES),
  },
  {
    path: 'styles',
    canActivate: [permissionGuard('catalog.read')],
    loadChildren: () => import('@features/catalog/styles/styles.routes').then((m) => m.STYLES_ROUTES),
  },
  {
    path: 'catalog',
    canActivate: [permissionGuard('catalog.read')],
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
    canActivate: [permissionGuard('procurement.read')],
    loadChildren: () => import('@features/vendor/vendors/vendors.routes').then((m) => m.VENDORS_ROUTES),
  },
  {
    path: 'production',
    canActivate: [permissionGuard('production.read')],
    loadChildren: () => import('@features/production/production-runs/production-runs.routes').then((m) => m.PRODUCTION_RUNS_ROUTES),
  },
  {
    path: 'quality',
    canActivate: [permissionGuard('qc.read')],
    loadChildren: () => import('@features/quality/batches/batches.routes').then((m) => m.BATCHES_ROUTES),
  },
  {
    path: 'labelling',
    canActivate: [permissionGuard('labelling.read')],
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
    canActivate: [permissionGuard('warehouse.read')],
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
        canActivate: [permissionGuard('inventory.read')],
        loadChildren: () => import('@features/warehouse/adjustments/adjustments.routes').then((m) => m.ADJUSTMENTS_ROUTES),
      },
      {
        path: 'cycle-counts',
        canActivate: [permissionGuard('inventory.read')],
        loadChildren: () => import('@features/warehouse/cycle-counts/cycle-counts.routes').then((m) => m.CYCLE_COUNTS_ROUTES),
      },
      {
        path: 'alerts',
        canActivate: [permissionGuard('inventory.read')],
        loadChildren: () => import('@features/warehouse/stock-alerts/stock-alerts.routes').then((m) => m.STOCK_ALERTS_ROUTES),
      },
      {
        path: 'stock',
        canActivate: [permissionGuard('inventory.read')],
        loadChildren: () => import('@features/warehouse/stock-ledger/stock-ledger.routes').then((m) => m.STOCK_LEDGER_ROUTES),
      },
    ],
  },
  {
    // Before 'storefront': its prefix would otherwise claim /storefront/search for the settings screens.
    path: 'storefront/search',
    canActivate: [permissionGuard('catalog.read')],
    loadChildren: () => import('@features/storefront/search/search.routes').then((m) => m.SEARCH_ROUTES),
  },
  {
    path: 'storefront',
    canActivate: [permissionGuard('catalog.read')],
    loadChildren: () => import('@features/storefront/storefront-settings/storefront-settings.routes').then((m) => m.STOREFRONT_SETTINGS_ROUTES),
  },
  {
    path: 'purchase-orders',
    canActivate: [permissionGuard('procurement.read')],
    loadChildren: () => import('@features/vendor/purchase-orders/purchase-orders.routes').then((m) => m.PURCHASE_ORDERS_ROUTES),
  },
];

/**
 * The sign-in, change-password and two-step setup pages sit outside the staff screens; every staff screen is behind it (sessions AC-9). The
 * guard lets the vendor-facing purchase order page through, because a vendor opens it with a link and never signs in as staff.
 */
export const routes: Routes = [...SESSIONS_ROUTES, ...MFA_SETUP_ROUTES, { path: '', canActivate: [authGuard], children: [...staffRoutes, ...ROLES_ROUTES, ...MFA_SECURITY_ROUTES] }];
