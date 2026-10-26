import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import * as staffEffects from './store/staff.effects';
import { staffReducer } from './store/staff.reducer';
import { STAFF_FEATURE_KEY } from './store/staff.state';

export const STAFF_ACCOUNTS_ROUTES: Routes = [
  {
    path: '',
    providers: [provideState(STAFF_FEATURE_KEY, staffReducer), provideEffects(staffEffects)],
    loadComponent: () => import('./containers/staff-page/staff-page.container').then((m) => m.StaffPageContainer),
    title: 'Staff · Romp Admin',
  },
];
