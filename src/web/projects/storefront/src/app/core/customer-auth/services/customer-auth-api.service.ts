import { HttpClient, HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import {
  AUTH_BASE,
  AuthConfig,
  AuthSession,
  CSRF_HEADER,
  RegisterInput,
  RegisterResult,
  ResetResult,
  SessionResult,
  SignInInput,
  SignInResult,
  SimpleResult,
} from '../models/customer-auth.model';

interface Problem {
  readonly unmetRules?: readonly string[];
  readonly captchaRequired?: boolean;
  readonly errors?: Record<string, string[] | string>;
}

const problem = (e: unknown): Problem => ((e as HttpErrorResponse).error ?? {}) as Problem;
const statusOf = (e: unknown): number => (e instanceof HttpErrorResponse ? e.status : 0);

/** HTTP only, through the same-origin pass-through in server.ts (ADR 0023). Every failure becomes a result; nothing throws. */
@Injectable({ providedIn: 'root' })
export class CustomerAuthApi {
  private readonly http = inject(HttpClient);

  session(): Observable<SessionResult> {
    return this.http.get<AuthSession>(`${AUTH_BASE}/session`).pipe(
      map((session): SessionResult => ({ status: 'ok', session })),
      catchError((e: unknown) => of<SessionResult>(statusOf(e) === 401 ? { status: 'signedOut' } : { status: 'error' })),
    );
  }

  signIn(input: SignInInput): Observable<SignInResult> {
    return this.http.post<AuthSession>(`${AUTH_BASE}/sign-in`, input).pipe(
      map((session): SignInResult => ({ status: 'ok', session })),
      catchError((e: unknown): Observable<SignInResult> => {
        if (statusOf(e) === 429) {
          const seconds = Number((e as HttpErrorResponse).headers.get('retry-after'));
          return of({ status: 'throttled', retryAfterSeconds: Number.isFinite(seconds) && seconds > 0 ? seconds : 30 });
        }
        if (statusOf(e) === 401) return of({ status: 'failed', captchaRequired: problem(e).captchaRequired === true });
        return of({ status: 'error' });
      }),
    );
  }

  register(input: RegisterInput): Observable<RegisterResult> {
    return this.http.post<AuthSession>(`${AUTH_BASE}/register`, input).pipe(
      map((session): RegisterResult => ({ status: 'ok', session })),
      catchError((e: unknown): Observable<RegisterResult> => {
        if (statusOf(e) === 409) return of({ status: 'emailTaken' });
        if (statusOf(e) === 422) return of({ status: 'passwordRejected', unmetRules: problem(e).unmetRules ?? [] });
        if (statusOf(e) === 400) return of({ status: 'invalid', errors: fieldErrors(problem(e).errors) });
        return of({ status: 'error' });
      }),
    );
  }

  signOut(): Observable<void> {
    return this.http.post<void>(`${AUTH_BASE}/sign-out`, null).pipe(catchError(() => of(undefined)));
  }

  verifyEmail(token: string): Observable<SimpleResult> {
    return this.simple(this.http.post<void>(`${AUTH_BASE}/email/verify`, { token }));
  }

  resendVerification(csrfToken: string): Observable<SimpleResult> {
    const headers = new HttpHeaders({ [CSRF_HEADER]: csrfToken });
    return this.simple(this.http.post<void>(`${AUTH_BASE}/email/resend`, null, { headers }));
  }

  requestReset(email: string): Observable<SimpleResult> {
    return this.simple(this.http.post<void>(`${AUTH_BASE}/password-reset/request`, { email }));
  }

  confirmReset(token: string, newPassword: string): Observable<ResetResult> {
    return this.http.post<void>(`${AUTH_BASE}/password-reset/confirm`, { token, newPassword }).pipe(
      map((): ResetResult => ({ status: 'ok' })),
      catchError((e: unknown): Observable<ResetResult> => {
        if (statusOf(e) === 422) return of({ status: 'passwordRejected', unmetRules: problem(e).unmetRules ?? [] });
        if (statusOf(e) === 400) return of({ status: 'linkInvalid' });
        return of({ status: 'error' });
      }),
    );
  }

  config(): Observable<AuthConfig> {
    return this.http.get<AuthConfig>('/customer-api/config').pipe(catchError(() => of<AuthConfig>({ turnstileSiteKey: '' })));
  }

  private simple(call: Observable<unknown>): Observable<SimpleResult> {
    return call.pipe(
      map((): SimpleResult => ({ status: 'ok' })),
      catchError((e: unknown): Observable<SimpleResult> => of(statusOf(e) === 400 ? { status: 'invalid' } : statusOf(e) === 429 ? { status: 'tooMany' } : { status: 'error' })),
    );
  }
}

function fieldErrors(errors: Problem['errors']): Record<string, string> {
  const out: Record<string, string> = {};
  for (const [key, value] of Object.entries(errors ?? {})) {
    const message = Array.isArray(value) ? value[0] : value;
    if (typeof message === 'string') out[key.charAt(0).toLowerCase() + key.slice(1)] = message;
  }
  return out;
}
