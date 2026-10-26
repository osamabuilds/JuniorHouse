import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of, switchMap, tap } from 'rxjs';
import { SessionPageActions } from '@core/auth';
import { ApiError } from '@core/http';
import { MfaApiService } from '../services/mfa-api.service';
import { MfaApiActions, MfaPageActions } from './mfa.actions';

/** Opening the enrolment page (or asking for a new secret) makes a new unconfirmed secret on the server. */
export const startEnrolment$ = createEffect(
  (actions$ = inject(Actions), api = inject(MfaApiService)) =>
    actions$.pipe(
      ofType(MfaPageActions.opened, MfaPageActions.restartClicked),
      switchMap(() =>
        api.start().pipe(
          map((enrolment) => MfaApiActions.startSucceeded({ enrolment })),
          catchError((error: ApiError) => of(MfaApiActions.startFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const confirmEnrolment$ = createEffect(
  (actions$ = inject(Actions), api = inject(MfaApiService)) =>
    actions$.pipe(
      ofType(MfaPageActions.codeSubmitted),
      switchMap(({ code }) =>
        api.confirm(code).pipe(
          map((result) => MfaApiActions.confirmSucceeded({ codes: result.recoveryCodes })),
          catchError((error: ApiError) => of(MfaApiActions.confirmFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The account now has a second factor: reload who the person is, so the session moves to the full scope. */
export const reloadSessionAfterEnrolment$ = createEffect(
  (actions$ = inject(Actions)) =>
    actions$.pipe(
      ofType(MfaApiActions.confirmSucceeded),
      map(() => SessionPageActions.started()),
    ),
  { functional: true },
);

export const goHomeWhenCodesSaved$ = createEffect(
  (actions$ = inject(Actions), router = inject(Router)) =>
    actions$.pipe(
      ofType(MfaPageActions.codesSaved),
      tap(() => void router.navigateByUrl('/')),
    ),
  { functional: true, dispatch: false },
);

export const loadStatus$ = createEffect(
  (actions$ = inject(Actions), api = inject(MfaApiService)) =>
    actions$.pipe(
      ofType(MfaPageActions.statusOpened, MfaApiActions.regenerateSucceeded),
      switchMap(() =>
        api.status().pipe(
          map((status) => MfaApiActions.statusLoaded({ status })),
          catchError((error: ApiError) => of(MfaApiActions.statusFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const regenerateCodes$ = createEffect(
  (actions$ = inject(Actions), api = inject(MfaApiService)) =>
    actions$.pipe(
      ofType(MfaPageActions.regenerateSubmitted),
      switchMap(({ code }) =>
        api.regenerate(code).pipe(
          map((result) => MfaApiActions.regenerateSucceeded({ codes: result.recoveryCodes })),
          catchError((error: ApiError) => of(MfaApiActions.regenerateFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
