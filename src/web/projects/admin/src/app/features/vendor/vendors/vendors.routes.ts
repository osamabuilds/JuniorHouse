import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as vendorsEffects from './store/vendors.effects';
import { vendorsReducer } from './store/vendors.reducer';
import { VENDORS_FEATURE_KEY } from './store/vendors.state';

export const VENDORS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(VENDORS_FEATURE_KEY, vendorsReducer), provideEffects(vendorsEffects)],
    loadComponent: () => import('./containers/vendors-page/vendors-page.container').then((m) => m.VendorsPageContainer),
    title: 'Vendors · Romp Admin',
  },
];
