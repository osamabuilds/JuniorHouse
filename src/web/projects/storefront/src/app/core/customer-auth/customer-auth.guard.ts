import { isPlatformServer } from '@angular/common';
import { PLATFORM_ID, inject } from '@angular/core';
import { toObservable } from '@angular/core/rxjs-interop';
import { CanActivateFn, Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { filter, map, take } from 'rxjs';
import { selectAuthStatus } from './store/customer-auth.selectors';

/**
 * The account pages are for a signed-in customer. Once the session answer is in, anyone else is sent to the sign-in page with a way back
 * (ACT-4). The API still refuses every call that has no session, so this guard is a courtesy, not the protection (SEC-05).
 */
export const signedInGuard: CanActivateFn = (_route, state) => {
  // The server render cannot renew an expired access cookie, so it lets the page render and the browser decides once it has renewed it.
  if (isPlatformServer(inject(PLATFORM_ID))) return true;
  const router = inject(Router);
  return toObservable(inject(Store).selectSignal(selectAuthStatus)).pipe(
    filter((status) => status !== 'unknown'),
    take(1),
    map((status) => (status === 'signedIn' ? true : router.createUrlTree(['/account/sign-in'], { queryParams: { returnUrl: state.url } }))),
  );
};

/** The sign-in and register pages are for someone who is not signed in; a signed-in customer goes to their account. */
export const signedOutGuard: CanActivateFn = () => {
  const router = inject(Router);
  return toObservable(inject(Store).selectSignal(selectAuthStatus)).pipe(
    filter((status) => status !== 'unknown'),
    take(1),
    map((status) => (status === 'signedIn' ? router.createUrlTree(['/account']) : true)),
  );
};
