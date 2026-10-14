import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as adjustmentsEffects from './store/adjustments.effects';
import { adjustmentsReducer } from './store/adjustments.reducer';
import { ADJUSTMENTS_FEATURE_KEY } from './store/adjustments.state';

export const ADJUSTMENTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(ADJUSTMENTS_FEATURE_KEY, adjustmentsReducer), provideEffects(adjustmentsEffects)],
    loadComponent: () => import('./containers/adjustments-page/adjustments-page.container').then((m) => m.AdjustmentsPageContainer),
    title: 'Adjustments · Romp Admin',
  },
];
