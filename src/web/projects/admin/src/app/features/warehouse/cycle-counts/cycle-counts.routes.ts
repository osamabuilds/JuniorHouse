import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as cycleCountsEffects from './store/cycle-counts.effects';
import { cycleCountsReducer } from './store/cycle-counts.reducer';
import { CYCLE_COUNTS_FEATURE_KEY } from './store/cycle-counts.state';

/** One store serves the list and the count page, so the notice "Count started" survives the move from one to the other. */
export const CYCLE_COUNTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(CYCLE_COUNTS_FEATURE_KEY, cycleCountsReducer), provideEffects(cycleCountsEffects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/cycle-counts-page/cycle-counts-page.container').then((m) => m.CycleCountsPageContainer),
        title: 'Cycle counts · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/cycle-count-page/cycle-count-page.container').then((m) => m.CycleCountPageContainer),
        title: 'Cycle count · Romp Admin',
      },
    ],
  },
];
