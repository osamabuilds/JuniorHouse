import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of, switchMap } from 'rxjs';
import { ApiError } from '@core/http';
import { RolesApiService } from '../services/roles-api.service';
import { RolesApiActions, RolesPageActions } from './roles.actions';

export const loadMatrix$ = createEffect(
  (actions$ = inject(Actions), api = inject(RolesApiService)) =>
    actions$.pipe(
      ofType(RolesPageActions.opened),
      switchMap(() =>
        api.matrix().pipe(
          map((matrix) => RolesApiActions.loadSucceeded({ matrix })),
          catchError((error: ApiError) => of(RolesApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
