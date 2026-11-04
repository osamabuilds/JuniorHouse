import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as discountCodesEffects from './store/discount-codes.effects';
import { discountCodesReducer } from './store/discount-codes.reducer';
import { DISCOUNT_CODES_FEATURE_KEY } from './store/discount-codes.state';

export const DISCOUNT_CODES_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(DISCOUNT_CODES_FEATURE_KEY, discountCodesReducer), provideEffects(discountCodesEffects)],
    loadComponent: () => import('./containers/discount-codes-page/discount-codes-page.container').then((m) => m.DiscountCodesPageContainer),
    title: 'Discount codes · Romp Admin',
  },
];
