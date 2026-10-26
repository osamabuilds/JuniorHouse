import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Store, createSelector } from '@ngrx/store';
import { filter, map, take } from 'rxjs';
import { SessionScope } from './auth.model';
import { selectSessionStatus, selectSessionUser } from './store/auth.selectors';

export const CHANGE_PASSWORD_PATH = '/change-password';
export const SETUP_REQUIRED_PATH = '/setup-required';
/** The vendor-facing purchase order page is opened by a vendor with a link, never by staff, so it asks for no staff sign-in. */
const VENDOR_VIEW_SEGMENT = '/vendor-view';

const selectGuardState = createSelector(selectSessionStatus, selectSessionUser, (status, user) => ({ status, scope: user?.scope ?? null }));

/** The one page a limited session may see; null when the session is full. */
const limitedPage = (scope: SessionScope | null): string | null =>
  scope === 'ChangePassword' ? CHANGE_PASSWORD_PATH : scope === 'EnrolMfa' ? SETUP_REQUIRED_PATH : null;

/**
 * Every staff screen needs a signed-in person (sessions AC-9). Anonymous visitors go to the sign-in page, remembering where they were
 * going; a session that is still limited (it owes a password change or two-step setup) can reach only that step, and a full session
 * cannot sit on those pages. The server enforces the same rules on every request; this only keeps the screens honest.
 */
export const authGuard: CanActivateFn = (_route, state) => {
  if (state.url.includes(VENDOR_VIEW_SEGMENT)) {
    return true;
  }

  const store = inject(Store);
  const router = inject(Router);

  return store.select(selectGuardState).pipe(
    filter(({ status }) => status !== 'unknown'), // wait for the first /me answer instead of flashing the sign-in page
    take(1),
    map(({ status, scope }) => {
      if (status === 'anonymous') {
        return router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
      }

      const required = limitedPage(scope);
      const path = state.url.split(/[?#]/)[0];
      if (required === null) {
        return path === CHANGE_PASSWORD_PATH || path === SETUP_REQUIRED_PATH ? router.parseUrl('/') : true;
      }

      return path === required ? true : router.parseUrl(required);
    }),
  );
};
