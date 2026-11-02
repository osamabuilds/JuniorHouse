import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as shippingEffects from './store/shipping.effects';
import { shippingReducer } from './store/shipping.reducer';
import { SHIPPING_FEATURE_KEY } from './store/shipping.state';

export const SHIPPING_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(SHIPPING_FEATURE_KEY, shippingReducer), provideEffects(shippingEffects)],
    loadComponent: () => import('./containers/shipping-page/shipping-page.container').then((m) => m.ShippingPageContainer),
    title: 'Delivery fees · Romp Admin',
  },
];
