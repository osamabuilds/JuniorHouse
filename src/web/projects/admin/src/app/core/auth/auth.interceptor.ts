import { HttpClient, HttpContextToken, HttpErrorResponse, HttpEvent, HttpHandlerFn, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Store } from '@ngrx/store';
import { Observable, catchError, finalize, map, of, shareReplay, switchMap, throwError } from 'rxjs';
import { API_BASE_URL } from '../config/api-base-url.token';
import { SessionPageActions } from './store/auth.actions';
import { selectCsrfToken } from './store/auth.selectors';

const CSRF_HEADER = 'X-Romp-Csrf';
const UNSAFE_METHODS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
const AUTH_PREFIX = '/api/auth/';
/** The auth routes that must never trigger a refresh: they are how a session starts, ends or is renewed. */
const NO_REFRESH = ['/api/auth/me', '/api/auth/sign-in', '/api/auth/refresh'];

/** Marks the repeat of a request after a refresh, so a second 401 is returned instead of looping. */
const REPEATED = new HttpContextToken<boolean>(() => false);

/**
 * Renews the session cookies. Requests that fail at the same moment share one call, because the refresh value is single use: a
 * second call with the old value would be refused (sessions AC-2, AC-3).
 */
@Injectable({ providedIn: 'root' })
export class SessionRefresher {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private inFlight: Observable<void> | null = null;

  refresh(): Observable<void> {
    this.inFlight ??= this.http.post(`${this.baseUrl}/api/auth/refresh`, {}).pipe(
      map(() => undefined),
      // 409: another request rotated the cookies a moment ago, so the browser already holds the new pair.
      catchError((error: HttpErrorResponse) => (error.status === 409 ? of(undefined) : throwError(() => error))),
      finalize(() => (this.inFlight = null)),
      shareReplay({ bufferSize: 1, refCount: false }),
    );
    return this.inFlight;
  }
}

/**
 * The browser half of the session (sessions AC-2, AC-10): sends the anti-forgery value on unsafe requests to this API, and on a 401
 * renews the session once and repeats the request. If renewing fails the person has to sign in again. The cookies themselves are
 * HttpOnly, so no script ever sees them; this code only ever holds the anti-forgery value, in memory.
 */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const baseUrl = inject(API_BASE_URL);
  if (!request.url.startsWith(`${baseUrl}/api/`)) {
    return next(request); // another site never receives the anti-forgery value
  }

  const store = inject(Store);
  const router = inject(Router);
  const refresher = inject(SessionRefresher);

  const send = (outgoing: HttpRequest<unknown>): Observable<HttpEvent<unknown>> => next(withCsrf(outgoing, store.selectSignal(selectCsrfToken)(), baseUrl));

  return send(request).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401 || request.context.get(REPEATED) || isNoRefresh(request.url, baseUrl)) {
        return throwError(() => error);
      }

      return refresher.refresh().pipe(
        switchMap(() => send(request.clone({ context: request.context.set(REPEATED, true) }))),
        catchError((refreshError: unknown) => {
          if (isRefreshFailure(refreshError, error)) {
            store.dispatch(SessionPageActions.sessionExpired({ returnUrl: router.url }));
          }
          return throwError(() => (refreshError instanceof HttpErrorResponse && refreshError.url?.endsWith('/api/auth/refresh') ? error : refreshError));
        }),
      );
    }),
  );
};

function withCsrf(request: HttpRequest<unknown>, token: string | null, baseUrl: string): HttpRequest<unknown> {
  const isSignIn = request.url === `${baseUrl}${AUTH_PREFIX}sign-in`;
  if (!token || isSignIn || !UNSAFE_METHODS.has(request.method)) {
    return request;
  }

  return request.clone({ setHeaders: { [CSRF_HEADER]: token } });
}

function isNoRefresh(url: string, baseUrl: string): boolean {
  return NO_REFRESH.some((path) => url === `${baseUrl}${path}`);
}

/** A failed refresh ends the session; a 401 on the repeated request (the refresh itself worked) does not. */
function isRefreshFailure(error: unknown, original: HttpErrorResponse): boolean {
  return error instanceof HttpErrorResponse && error !== original && !!error.url?.endsWith('/api/auth/refresh');
}
