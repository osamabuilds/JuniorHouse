import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, concatMap, map, of, switchMap } from 'rxjs';
import { ApiError } from '@core/http';
import { PackingSlipsApiService } from '../services/packing-slips-api.service';
import { PackingSlipApiActions, PackingSlipPageActions } from './packing-slips.actions';

export const loadSlip$ = createEffect(
  (actions$ = inject(Actions), api = inject(PackingSlipsApiService)) =>
    actions$.pipe(
      ofType(PackingSlipPageActions.opened),
      switchMap(({ orderId }) =>
        api.get(orderId).pipe(
          map((slip) => PackingSlipApiActions.loadSucceeded({ slip })),
          catchError((error: ApiError) => of(PackingSlipApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const print$ = createEffect(
  (actions$ = inject(Actions), api = inject(PackingSlipsApiService)) =>
    actions$.pipe(
      ofType(PackingSlipPageActions.printClicked),
      concatMap(({ orderId }) =>
        api.print(orderId).pipe(
          map((result) => PackingSlipApiActions.printSucceeded({ result })),
          catchError((error: ApiError) => of(PackingSlipApiActions.printFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
