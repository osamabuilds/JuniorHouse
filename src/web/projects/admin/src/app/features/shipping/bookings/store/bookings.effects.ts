import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { BookingsApiService } from '../services/bookings-api.service';
import { BookingsApiActions, ShipmentDetailPageActions, ShipmentsPageActions } from './bookings.actions';
import { selectQuery } from './bookings.selectors';

export const loadRows$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(BookingsApiService)) =>
    actions$.pipe(
      ofType(ShipmentsPageActions.opened, ShipmentsPageActions.statusChosen, ShipmentsPageActions.pageChanged, ShipmentsPageActions.pageSizeChanged, ShipmentsPageActions.refreshed),
      withLatestFrom(store.select(selectQuery)),
      switchMap(([, q]) =>
        api.list(q.status, q.page, q.pageSize).pipe(
          map((result) => BookingsApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(BookingsApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The detail is read when the page opens and again after a retry, so the screen shows the truth. */
export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(BookingsApiService)) =>
    actions$.pipe(
      ofType(ShipmentDetailPageActions.opened, BookingsApiActions.retrySucceeded),
      switchMap((action) =>
        api.get(action.id).pipe(
          map((detail) => BookingsApiActions.detailSucceeded({ detail })),
          catchError((error: ApiError) => of(BookingsApiActions.detailFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** "Try again now" makes the parcel due and books it once; a repeat press while it runs does nothing more (MBQ-3). */
export const retry$ = createEffect(
  (actions$ = inject(Actions), api = inject(BookingsApiService)) =>
    actions$.pipe(
      ofType(ShipmentDetailPageActions.retryClicked),
      concatMap(({ id }) =>
        api.retry(id).pipe(
          map((result) => BookingsApiActions.retrySucceeded({ outcome: result.outcome, id })),
          catchError((error: ApiError) => of(BookingsApiActions.retryFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
