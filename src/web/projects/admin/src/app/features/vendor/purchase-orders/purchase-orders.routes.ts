import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as poWorkflowEffects from './store/po-workflow.effects';
import * as purchaseOrdersEffects from './store/purchase-orders.effects';
import { purchaseOrdersReducer } from './store/purchase-orders.reducer';
import { PURCHASE_ORDERS_FEATURE_KEY } from './store/purchase-orders.state';
import * as vendorPoViewEffects from './store/vendor-po-view.effects';

export const PURCHASE_ORDERS_ROUTES: Routes = [
  {
    path: '',
    providers: [
      provideState(PURCHASE_ORDERS_FEATURE_KEY, purchaseOrdersReducer),
      provideEffects(purchaseOrdersEffects, poWorkflowEffects, vendorPoViewEffects),
    ],
    children: [
      {
        // SCRUM-93 task 51: what a vendor sees of a PO. The shell hides its navigation on this route.
        path: ':id/vendor-view',
        loadComponent: () =>
          import('./containers/vendor-view-page/vendor-view-page.container').then((m) => m.VendorViewPageContainer),
        title: 'Purchase Order · Romp',
      },
      {
        path: '',
        loadComponent: () =>
          import('./containers/purchase-orders-page/purchase-orders-page.container').then((m) => m.PurchaseOrdersPageContainer),
        title: 'Purchase Orders · Romp Admin',
      },
    ],
  },
];
