import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as pickListsEffects from './store/pick-lists.effects';
import { pickListsReducer } from './store/pick-lists.reducer';
import { PICK_LISTS_FEATURE_KEY } from './store/pick-lists.state';

export const PICK_LISTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(PICK_LISTS_FEATURE_KEY, pickListsReducer), provideEffects(pickListsEffects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/pick-queue-page/pick-queue-page.container').then((m) => m.PickQueuePageContainer),
        title: 'Pick queue · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/pick-station-page/pick-station-page.container').then((m) => m.PickStationPageContainer),
        title: 'Pick order · Romp Admin',
      },
    ],
  },
];
