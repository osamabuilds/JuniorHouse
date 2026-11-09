import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as labelsEffects from './store/labels.store';
import { LABELS_FEATURE_KEY, labelsReducer } from './store/labels.store';

export const LABELS_ROUTES: Routes = [
  {
    path: ':id',
    providers: [provideState(LABELS_FEATURE_KEY, labelsReducer), provideEffects({ loadLabel$: labelsEffects.loadLabel$, recordPrinted$: labelsEffects.recordPrinted$ })],
    loadComponent: () => import('./containers/label-page/label-page.container').then((m) => m.LabelPageContainer),
    title: 'Shipping label · Romp Admin',
  },
];
