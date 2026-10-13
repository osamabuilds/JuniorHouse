import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as productionEffects from './store/production.effects';
import { productionReducer } from './store/production.reducer';
import { PRODUCTION_FEATURE_KEY } from './store/production.state';

export const PRODUCTION_RUNS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(PRODUCTION_FEATURE_KEY, productionReducer), provideEffects(productionEffects)],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./containers/production-runs-page/production-runs-page.container').then((m) => m.ProductionRunsPageContainer),
        title: 'Production · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () =>
          import('./containers/production-run-page/production-run-page.container').then((m) => m.ProductionRunPageContainer),
        title: 'Production run · Romp Admin',
      },
    ],
  },
];
