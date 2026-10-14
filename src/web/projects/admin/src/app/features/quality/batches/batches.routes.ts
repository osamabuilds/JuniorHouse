import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as qualityEffects from './store/quality.effects';
import { qualityReducer } from './store/quality.reducer';
import { QUALITY_FEATURE_KEY } from './store/quality.state';

export const BATCHES_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(QUALITY_FEATURE_KEY, qualityReducer), provideEffects(qualityEffects)],
    children: [
      {
        path: '',
        loadComponent: () =>
          import('./containers/receiving-queue-page/receiving-queue-page.container').then((m) => m.ReceivingQueuePageContainer),
        title: 'Receiving · Romp Admin',
      },
      {
        path: 'receive/:id',
        loadComponent: () => import('./containers/receive-page/receive-page.container').then((m) => m.ReceivePageContainer),
        title: 'Receive goods · Romp Admin',
      },
      {
        path: 'batches',
        loadComponent: () => import('./containers/batches-page/batches-page.container').then((m) => m.BatchesPageContainer),
        title: 'QC batches · Romp Admin',
      },
      {
        path: 'batches/:id',
        loadComponent: () => import('./containers/batch-page/batch-page.container').then((m) => m.BatchPageContainer),
        title: 'QC batch · Romp Admin',
      },
    ],
  },
];
