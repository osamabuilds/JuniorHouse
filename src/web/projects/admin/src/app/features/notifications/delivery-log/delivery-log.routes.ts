import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as deliveryLogEffects from './store/delivery-log.effects';
import { deliveryLogReducer } from './store/delivery-log.reducer';
import { DELIVERY_LOG_FEATURE_KEY } from './store/delivery-log.state';

export const DELIVERY_LOG_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(DELIVERY_LOG_FEATURE_KEY, deliveryLogReducer), provideEffects(deliveryLogEffects)],
    loadComponent: () => import('./containers/delivery-log-page/delivery-log-page.container').then((m) => m.DeliveryLogPageContainer),
    title: 'Notifications · Romp Admin',
  },
];
