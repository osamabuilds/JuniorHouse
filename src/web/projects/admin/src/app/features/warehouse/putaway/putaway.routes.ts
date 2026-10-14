import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as putawayEffects from './store/putaway.effects';
import { putawayReducer } from './store/putaway.reducer';
import { PUTAWAY_FEATURE_KEY } from './store/putaway.state';

export const PUTAWAY_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(PUTAWAY_FEATURE_KEY, putawayReducer), provideEffects(putawayEffects)],
    loadComponent: () => import('./containers/putaway-page/putaway-page.container').then((m) => m.PutawayPageContainer),
    title: 'Putaway · Romp Admin',
  },
];
