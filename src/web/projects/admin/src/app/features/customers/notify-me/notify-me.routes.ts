import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as notifyDemandEffects from './store/notify-demand.effects';
import { notifyDemandReducer } from './store/notify-demand.reducer';
import { NOTIFY_DEMAND_FEATURE_KEY } from './store/notify-demand.state';

export const NOTIFY_ME_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(NOTIFY_DEMAND_FEATURE_KEY, notifyDemandReducer), provideEffects(notifyDemandEffects)],
    loadComponent: () => import('./containers/notify-demand-page/notify-demand-page.container').then((m) => m.NotifyDemandPageContainer),
    title: 'Notify Me demand · Romp Admin',
  },
];
