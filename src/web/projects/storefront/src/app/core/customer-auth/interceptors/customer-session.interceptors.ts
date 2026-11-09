import { HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { isPlatformBrowser } from '@angular/common';
import { PLATFORM_ID, REQUEST, inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { SessionRefresher } from '../services/session-refresher.service';

const OWN_API = '/customer-api/';
const AUTH = '/customer-api/auth/';

/**
 * Browser: a call to the customer API that finds the access cookie expired (401) renews the cookies once and repeats the call. The
 * sign-in routes themselves are left alone, because a 401 there is a real answer ("could not sign you in"), not an expired session.
 */
export const customerRefreshInterceptor: HttpInterceptorFn = (req, next) => {
  const refresher = inject(SessionRefresher);
  if (!isPlatformBrowser(inject(PLATFORM_ID)) || !req.url.startsWith(OWN_API) || req.url.startsWith(AUTH) || req.url === '/customer-api/config') return next(req);
  return next(req).pipe(
    catchError((error: { status?: number }) =>
      error.status === 401 ? refresher.refresh().pipe(switchMap((renewed) => (renewed ? next(req.clone()) : throwError(() => error)))) : throwError(() => error),
    ),
  );
};

/**
 * Server render: the browser's cookies go along on calls to this site's own customer API, so a signed-in customer is already signed in
 * in the HTML. Nothing else gets them, and the server never renews cookies (see the effects).
 */
export const serverCookieInterceptor: HttpInterceptorFn = (req: HttpRequest<unknown>, next) => {
  const cookie = inject(REQUEST, { optional: true })?.headers.get('cookie');
  return next(cookie && req.url.startsWith(OWN_API) ? req.clone({ setHeaders: { cookie } }) : req);
};
