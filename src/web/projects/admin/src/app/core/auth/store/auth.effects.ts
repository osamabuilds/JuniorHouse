import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, EMPTY, map, of, switchMap, tap } from 'rxjs';
import { ApiError } from '../../http/api-error.model';
import { AuthApiService } from '../auth-api.service';
import { SessionApiActions, SessionPageActions } from './auth.actions';

const loadMe = (api: AuthApiService) =>
  api.me().pipe(
    map((user) => SessionApiActions.loadSucceeded({ user })),
    catchError(() => of(SessionApiActions.loadFailed())), // a 401 here just means nobody is signed in
  );

/** Asks who the caller is when the app starts. */
export const loadSession$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionPageActions.started),
      switchMap(() => loadMe(api)),
    ),
  { functional: true },
);

export const signIn$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionPageActions.signInSubmitted),
      // The password is in the action only long enough to be sent; the reducer never copies it into the state.
      switchMap(({ userName, password }) =>
        api.signIn({ userName, password }).pipe(
          map((response) => SessionApiActions.signInSucceeded({ response })),
          catchError((error: ApiError) => of(SessionApiActions.signInFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The second step of sign-in: the pending cookie travels by itself, so only the code is sent. */
export const verifyMfa$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionPageActions.mfaCodeSubmitted),
      switchMap(({ code }) =>
        api.mfaVerify(code).pipe(
          map(() => SessionApiActions.mfaVerified()),
          catchError((error: ApiError) => of(SessionApiActions.mfaFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadAfterMfa$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionApiActions.mfaVerified),
      switchMap(() => loadMe(api)),
    ),
  { functional: true },
);

/** A session was started (limited scope): find out who the person is and what they may do. */
export const loadAfterSignIn$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionApiActions.signInSucceeded),
      switchMap(({ response }) => (response.status === 'limited' ? loadMe(api) : EMPTY)),
    ),
  { functional: true },
);

export const changePassword$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionPageActions.changePasswordSubmitted),
      switchMap((value) =>
        api.changePassword({ currentPassword: value.currentPassword, newPassword: value.newPassword }).pipe(
          map(() => SessionApiActions.passwordChanged()),
          catchError((error: ApiError) => of(SessionApiActions.passwordChangeFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The password changed: reload the session so the page the person is allowed to see moves on. */
export const loadAfterPasswordChange$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionApiActions.passwordChanged),
      switchMap(() => loadMe(api)),
    ),
  { functional: true },
);

/** This browser forgets the session whether or not the server answered: the cookie is already cleared or the session is dead. */
export const signOut$ = createEffect(
  (actions$ = inject(Actions), api = inject(AuthApiService)) =>
    actions$.pipe(
      ofType(SessionPageActions.signOutClicked, SessionPageActions.signOutEverywhereClicked),
      switchMap((action) =>
        (action.type === SessionPageActions.signOutEverywhereClicked.type ? api.signOutEverywhere() : api.signOut()).pipe(
          map(() => SessionApiActions.signedOut()),
          catchError(() => of(SessionApiActions.signedOut())),
        ),
      ),
    ),
  { functional: true },
);

export const goToSignIn$ = createEffect(
  (actions$ = inject(Actions), router = inject(Router)) =>
    actions$.pipe(
      ofType(SessionApiActions.signedOut),
      tap(() => void router.navigate(['/sign-in'])),
    ),
  { functional: true, dispatch: false },
);

export const goToSignInAfterExpiry$ = createEffect(
  (actions$ = inject(Actions), router = inject(Router)) =>
    actions$.pipe(
      ofType(SessionPageActions.sessionExpired),
      tap(({ returnUrl }) => void router.navigate(['/sign-in'], { queryParams: { returnUrl } })),
    ),
  { functional: true, dispatch: false },
);
