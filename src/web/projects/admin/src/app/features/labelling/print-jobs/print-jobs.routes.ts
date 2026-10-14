import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as printJobsEffects from './store/print-jobs.effects';
import { printJobsReducer } from './store/print-jobs.reducer';
import { PRINT_JOBS_FEATURE_KEY } from './store/print-jobs.state';

export const PRINT_JOBS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(PRINT_JOBS_FEATURE_KEY, printJobsReducer), provideEffects(printJobsEffects)],
    loadComponent: () => import('./containers/print-queue-page/print-queue-page.container').then((m) => m.PrintQueuePageContainer),
    title: 'Print queue · Romp Admin',
  },
];
