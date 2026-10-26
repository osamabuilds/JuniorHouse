import { Routes } from '@angular/router';
import { provideEffects } from '@ngrx/effects';
import { provideState } from '@ngrx/store';
import { authGuard } from '@core/auth';
import * as mfaEffects from './store/mfa.effects';
import { mfaReducer } from './store/mfa.reducer';
import { MFA_FEATURE_KEY } from './store/mfa.state';

const providers = [provideState(MFA_FEATURE_KEY, mfaReducer), provideEffects(mfaEffects)];

/** Where a session that still owes two-step verification is kept (the auth guard sends it here). Outside the staff chrome. */
export const MFA_SETUP_ROUTES: Routes = [
  {
    path: 'setup-required',
    canActivate: [authGuard],
    providers,
    loadComponent: () => import('./containers/enrol-page/enrol-page.container').then((m) => m.EnrolPageContainer),
    title: 'Set up two-step verification · Romp Admin',
  },
];

/** The account's security page, inside the staff screens. */
export const MFA_SECURITY_ROUTES: Routes = [
  {
    path: 'security',
    providers,
    loadComponent: () => import('./containers/security-page/security-page.container').then((m) => m.SecurityPageContainer),
    title: 'Security · Romp Admin',
  },
];
