import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { Store, createSelector } from '@ngrx/store';
import { filter, map, take } from 'rxjs';
import { holdsAny, selectPermissions, selectSessionStatus } from './store/auth.selectors';

export const FORBIDDEN_PATH = '/forbidden';
const VENDOR_VIEW_SEGMENT = '/vendor-view';

const selectGuardState = createSelector(selectSessionStatus, selectPermissions, (status, permissions) => ({ status, permissions }));

/**
 * Keeps a screen to the people who hold a permission (roles AC-7). A person without it is sent to a "no access" page rather than shown
 * a screen whose every call would be refused. The server still checks each call; this only keeps the screens honest.
 */
export function permissionGuard(required: string | readonly string[]): CanActivateFn {
  const wanted = typeof required === 'string' ? [required] : required;

  return (_route, state) => {
    if (state.url.includes(VENDOR_VIEW_SEGMENT)) {
      return true; // the vendor-facing page asks for no staff sign-in, so it asks for no permission either
    }

    const store = inject(Store);
    const router = inject(Router);

    return store.select(selectGuardState).pipe(
      filter(({ status }) => status !== 'unknown'),
      take(1),
      map(({ status, permissions }) => {
        if (status === 'anonymous') {
          return router.createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });
        }

        return holdsAny(permissions, wanted) || state.url.startsWith(FORBIDDEN_PATH) ? true : router.parseUrl(FORBIDDEN_PATH);
      }),
    );
  };
}
