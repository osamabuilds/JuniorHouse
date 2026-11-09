import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as packingSlipsEffects from './store/packing-slips.effects';
import { packingSlipsReducer } from './store/packing-slips.reducer';
import { PACKING_SLIPS_FEATURE_KEY } from './store/packing-slips.state';

export const PACKING_SLIPS_ROUTES: Routes = [
  {
    path: ':id',
    providers: [provideState(PACKING_SLIPS_FEATURE_KEY, packingSlipsReducer), provideEffects(packingSlipsEffects)],
    loadComponent: () => import('./containers/packing-slip-page/packing-slip-page.container').then((m) => m.PackingSlipPageContainer),
    title: 'Packing slip · Romp Admin',
  },
];
