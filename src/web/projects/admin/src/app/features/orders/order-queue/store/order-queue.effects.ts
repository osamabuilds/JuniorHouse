import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { OrdersApiService } from '../services/orders-api.service';
import { OrderQueueActions, OrderQueueApiActions } from './order-queue.actions';
import { selectQuery } from './order-queue.selectors';

/** Re-runs the list when the page opens, the filters or paging change. */
export const loadOrders$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(OrdersApiService)) =>
    actions$.pipe(
      ofType(OrderQueueActions.listOpened, OrderQueueActions.filtersChanged, OrderQueueActions.pageChanged, OrderQueueActions.pageSizeChanged),
      withLatestFrom(store.select(selectQuery)),
      switchMap(([, q]) =>
        api.list(q.filters, q.page, q.pageSize).pipe(
          map((result) => OrderQueueApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(OrderQueueApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(OrdersApiService)) =>
    actions$.pipe(
      ofType(OrderQueueActions.detailOpened),
      switchMap(({ id }) =>
        api.get(id).pipe(
          map((order) => OrderQueueApiActions.detailSucceeded({ order })),
          catchError((error: ApiError) => of(OrderQueueApiActions.detailFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const recordOutcome$ = createEffect(
  (actions$ = inject(Actions), api = inject(OrdersApiService)) =>
    actions$.pipe(
      ofType(OrderQueueActions.outcomeRecorded),
      switchMap(({ id, outcome, note }) =>
        api.recordOutcome(id, outcome, note).pipe(
          map((order) => OrderQueueApiActions.actionSucceeded({ order, notice: outcomeNotice(outcome) })),
          catchError((error: ApiError) => of(OrderQueueApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const cancelOrder$ = createEffect(
  (actions$ = inject(Actions), api = inject(OrdersApiService)) =>
    actions$.pipe(
      ofType(OrderQueueActions.orderCancelled),
      switchMap(({ id, note }) =>
        api.cancel(id, note).pipe(
          map((order) => OrderQueueApiActions.actionSucceeded({ order, notice: 'The order was cancelled. Its stock and any discount use are given back.' })),
          catchError((error: ApiError) => of(OrderQueueApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const clearFlag$ = createEffect(
  (actions$ = inject(Actions), api = inject(OrdersApiService)) =>
    actions$.pipe(
      ofType(OrderQueueActions.flagCleared),
      switchMap(({ id }) =>
        api.clearFlag(id).pipe(
          map((order) => OrderQueueApiActions.actionSucceeded({ order, notice: 'The refusal flag was cleared.' })),
          catchError((error: ApiError) => of(OrderQueueApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

function outcomeNotice(outcome: string): string {
  switch (outcome) {
    case 'confirmed':
      return 'The order is confirmed.';
    case 'no-answer':
      return 'No answer was recorded. The next call is scheduled.';
    default:
      return 'The refusal was recorded and the order is cancelled.';
  }
}
