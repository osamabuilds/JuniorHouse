import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as binsEffects from './store/bins.effects';
import { binsReducer } from './store/bins.reducer';
import { BINS_FEATURE_KEY } from './store/bins.state';

export const BINS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(BINS_FEATURE_KEY, binsReducer), provideEffects(binsEffects)],
    loadComponent: () => import('./containers/bins-page/bins-page.container').then((m) => m.BinsPageContainer),
    title: 'Bins · Romp Admin',
  },
];
