import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as queueEffects from './store/queue.effects';
import { queueReducer } from './store/queue.reducer';
import { QUEUE_FEATURE_KEY } from './store/queue.state';

export const MANUAL_BOOKING_QUEUE_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(QUEUE_FEATURE_KEY, queueReducer), provideEffects(queueEffects)],
    loadComponent: () => import('./containers/queue-page/queue-page.container').then((m) => m.QueuePageContainer),
    title: 'Booking queue · Romp Admin',
  },
];
