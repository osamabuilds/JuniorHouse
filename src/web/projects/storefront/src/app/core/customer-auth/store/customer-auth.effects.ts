import { isPlatformBrowser } from '@angular/common';
import { Injectable, PLATFORM_ID, inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { concatMap, exhaustMap, filter, map, of, switchMap, withLatestFrom } from 'rxjs';
import { CustomerAuthApi } from '../services/customer-auth-api.service';
import { SessionRefresher } from '../services/session-refresher.service';
import { CustomerAuthActions } from './customer-auth.actions';
import { selectAuthSession } from './customer-auth.selectors';

@Injectable()
export class CustomerAuthEffects {
  private readonly actions$ = inject(Actions);
  private readonly api = inject(CustomerAuthApi);
  private readonly refresher = inject(SessionRefresher);
  private readonly store = inject(Store);
  private readonly browser = isPlatformBrowser(inject(PLATFORM_ID));

  /**
   * Who is signed in. An expired access cookie is renewed with the refresh cookie, in the browser only: the server render cannot pass new
   * cookies to the browser, and a refresh value that was already used is treated as stolen.
   */
  readonly loadSession$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.sessionRequested),
      switchMap(() =>
        this.api.session().pipe(
          switchMap((first) =>
            first.status === 'signedOut' && this.browser
              ? this.refresher.refresh().pipe(switchMap((renewed) => (renewed ? this.api.session() : of(first))))
              : of(first),
          ),
          map((result) => CustomerAuthActions.sessionLoaded({ result })),
        ),
      ),
    ),
  );

  /** exhaustMap: a double click does not send two attempts (each attempt counts towards the throttle). */
  readonly signIn$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.signInSubmitted),
      exhaustMap(({ input }) => this.api.signIn(input).pipe(map((result) => CustomerAuthActions.signInDone({ result })))),
    ),
  );

  readonly register$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.registerSubmitted),
      exhaustMap(({ input }) => this.api.register(input).pipe(map((result) => CustomerAuthActions.registerDone({ result })))),
    ),
  );

  readonly signOut$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.signOutSubmitted),
      exhaustMap(() => this.api.signOut().pipe(map(() => CustomerAuthActions.signOutDone()))),
    ),
  );

  readonly requestReset$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.resetRequested),
      exhaustMap(({ email }) => this.api.requestReset(email).pipe(map((result) => CustomerAuthActions.resetRequestDone({ result })))),
    ),
  );

  readonly confirmReset$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.resetConfirmed),
      exhaustMap(({ token, newPassword }) => this.api.confirmReset(token, newPassword).pipe(map((result) => CustomerAuthActions.resetConfirmDone({ result })))),
    ),
  );

  readonly verifyEmail$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.verifyEmailSubmitted),
      exhaustMap(({ token }) => this.api.verifyEmail(token).pipe(map((result) => CustomerAuthActions.verifyEmailDone({ result })))),
    ),
  );

  /** After a verification link works, read the session again so the notice bar agrees with the server. */
  readonly sessionAfterVerify$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.verifyEmailDone),
      filter(({ result }) => result.status === 'ok'),
      map(() => CustomerAuthActions.sessionRequested()),
    ),
  );

  readonly resend$ = createEffect(() =>
    this.actions$.pipe(
      ofType(CustomerAuthActions.resendVerificationSubmitted),
      withLatestFrom(this.store.select(selectAuthSession)),
      filter(([, session]) => session !== null),
      concatMap(([, session]) => this.api.resendVerification(session!.csrfToken).pipe(map((result) => CustomerAuthActions.resendVerificationDone({ result })))),
    ),
  );
}
