import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as orderQueueEffects from './store/order-queue.effects';
import { orderQueueReducer } from './store/order-queue.reducer';
import { ORDER_QUEUE_FEATURE_KEY } from './store/order-queue.state';

export const ORDER_QUEUE_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(ORDER_QUEUE_FEATURE_KEY, orderQueueReducer), provideEffects(orderQueueEffects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/orders-page/orders-page.container').then((m) => m.OrdersPageContainer),
        title: 'Orders · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/order-detail-page/order-detail-page.container').then((m) => m.OrderDetailPageContainer),
        title: 'Order · Romp Admin',
      },
    ],
  },
];
