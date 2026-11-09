import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as packStationEffects from './store/pack-station.effects';
import { packStationReducer } from './store/pack-station.reducer';
import { PACK_STATION_FEATURE_KEY } from './store/pack-station.state';

export const PACK_STATION_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(PACK_STATION_FEATURE_KEY, packStationReducer), provideEffects(packStationEffects)],
    children: [
      {
        path: '',
        loadComponent: () => import('./containers/pack-queue-page/pack-queue-page.container').then((m) => m.PackQueuePageContainer),
        title: 'Pack queue · Romp Admin',
      },
      {
        path: ':id',
        loadComponent: () => import('./containers/pack-station-page/pack-station-page.container').then((m) => m.PackStationPageContainer),
        title: 'Pack order · Romp Admin',
      },
    ],
  },
];
