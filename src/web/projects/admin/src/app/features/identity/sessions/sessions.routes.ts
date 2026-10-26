import { Routes } from '@angular/router';
import { authGuard } from '@core/auth';

/** The sign-in page is public; the change-password page needs a session (the guard also keeps each limited session on its own page). */
export const SESSIONS_ROUTES: Routes = [
  {
    path: 'sign-in',
    loadComponent: () => import('./containers/sign-in-page/sign-in-page.container').then((m) => m.SignInPageContainer),
    title: 'Sign in · Romp Admin',
  },
  {
    path: 'change-password',
    canActivate: [authGuard],
    loadComponent: () => import('./containers/change-password-page/change-password-page.container').then((m) => m.ChangePasswordPageContainer),
    title: 'Change password · Romp Admin',
  },
];
