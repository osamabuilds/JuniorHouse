import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { QueueApiService } from '../services/queue-api.service';
import { QueueApiActions, QueuePageActions } from './queue.actions';
import { selectQuery } from './queue.selectors';

export const loadRows$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(QueueApiService)) =>
    actions$.pipe(
      ofType(QueuePageActions.opened, QueuePageActions.filterChanged, QueuePageActions.pageChanged, QueuePageActions.pageSizeChanged, QueuePageActions.refreshed, QueueApiActions.actionSucceeded),
      withLatestFrom(store.select(selectQuery)),
      switchMap(([, q]) =>
        api.list(q.needsStaffOnly, q.page, q.pageSize).pipe(
          map((result) => QueueApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(QueueApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** "Try again now": the parcel is made due and booked once; a repeat press finds it already claimed (MBQ-3). */
export const retry$ = createEffect(
  (actions$ = inject(Actions), api = inject(QueueApiService)) =>
    actions$.pipe(
      ofType(QueuePageActions.retryClicked),
      concatMap(({ id }) =>
        api.retry(id).pipe(
          map((r) => QueueApiActions.actionSucceeded({ message: r.outcome === 'Booked' ? 'A courier booked the parcel.' : 'Tried again; it is still waiting.' })),
          catchError((error: ApiError) => of(QueueApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** A waybill from the courier by phone (MBQ-4). The server refuses one that is already on another parcel (MBQ-5). */
export const manualWaybill$ = createEffect(
  (actions$ = inject(Actions), api = inject(QueueApiService)) =>
    actions$.pipe(
      ofType(QueuePageActions.waybillEntered),
      concatMap(({ id, courier, waybill }) =>
        api.manualWaybill(id, courier, waybill).pipe(
          map(() => QueueApiActions.actionSucceeded({ message: 'The waybill is saved and the parcel is booked.' })),
          catchError((error: ApiError) => of(QueueApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
