import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as exceptionsEffects from './store/exceptions.effects';
import { exceptionsReducer } from './store/exceptions.reducer';
import { EXCEPTIONS_FEATURE_KEY } from './store/exceptions.state';

export const PICK_EXCEPTIONS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(EXCEPTIONS_FEATURE_KEY, exceptionsReducer), provideEffects(exceptionsEffects)],
    loadComponent: () => import('./containers/exceptions-page/exceptions-page.container').then((m) => m.ExceptionsPageContainer),
    title: 'Pick exceptions · Romp Admin',
  },
];
