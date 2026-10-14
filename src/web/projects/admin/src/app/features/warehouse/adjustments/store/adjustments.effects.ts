import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { AdjustmentApiService } from '../services/adjustment-api.service';
import { AdjustmentsApiActions, AdjustmentsPageActions } from './adjustments.actions';
import { selectFilters } from './adjustments.selectors';

/** Re-runs the history whenever a filter changes or a new adjustment changed what it shows. */
export const loadAdjustments$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(AdjustmentApiService)) =>
    actions$.pipe(
      ofType(
        AdjustmentsPageActions.opened,
        AdjustmentsPageActions.reasonFilterChanged,
        AdjustmentsPageActions.pageChanged,
        AdjustmentsPageActions.pageSizeChanged,
        AdjustmentsPageActions.refreshRequested,
        AdjustmentsApiActions.postSucceeded,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => AdjustmentsApiActions.loadAdjustmentsSucceeded({ result })),
          catchError((error: ApiError) => of(AdjustmentsApiActions.loadAdjustmentsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadReasons$ = createEffect(
  (actions$ = inject(Actions), api = inject(AdjustmentApiService)) =>
    actions$.pipe(
      ofType(AdjustmentsPageActions.opened),
      switchMap(() =>
        api.reasons().pipe(
          map((reasons) => AdjustmentsApiActions.loadReasonsSucceeded({ reasons })),
          catchError((error: ApiError) => of(AdjustmentsApiActions.loadReasonsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** exhaustMap-style safety is not needed: the idempotency key makes a repeat of the same attempt apply once (ADJ-8). */
export const post$ = createEffect(
  (actions$ = inject(Actions), api = inject(AdjustmentApiService)) =>
    actions$.pipe(
      ofType(AdjustmentsPageActions.submitted),
      switchMap(({ value }) =>
        api.post(value).pipe(
          map((adjustment) => AdjustmentsApiActions.postSucceeded({ adjustment })),
          catchError((error: ApiError) => of(AdjustmentsApiActions.postFailed({ error, value }))),
        ),
      ),
    ),
  { functional: true },
);
