import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { DeliveryLogApiService } from '../services/delivery-log-api.service';
import { DeliveryLogApiActions, DeliveryLogPageActions } from './delivery-log.actions';
import { selectQuery } from './delivery-log.selectors';

/** Re-runs the list when the page opens, the filters, the paging or a refresh asks for it. */
export const loadRows$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(DeliveryLogApiService)) =>
    actions$.pipe(
      ofType(
        DeliveryLogPageActions.opened,
        DeliveryLogPageActions.filtersChanged,
        DeliveryLogPageActions.pageChanged,
        DeliveryLogPageActions.pageSizeChanged,
        DeliveryLogPageActions.refreshed,
      ),
      withLatestFrom(store.select(selectQuery)),
      switchMap(([, q]) =>
        api.list(q.filters, q.page, q.pageSize).pipe(
          map((result) => DeliveryLogApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(DeliveryLogApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The strip is reloaded whenever the page opens or is refreshed; a resend refreshes it too (the queue changed). */
export const loadSummary$ = createEffect(
  (actions$ = inject(Actions), api = inject(DeliveryLogApiService)) =>
    actions$.pipe(
      ofType(DeliveryLogPageActions.opened, DeliveryLogPageActions.refreshed),
      switchMap(() =>
        api.summary().pipe(
          map((summary) => DeliveryLogApiActions.summarySucceeded({ summary })),
          catchError((error: ApiError) => of(DeliveryLogApiActions.summaryFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(DeliveryLogApiService)) =>
    actions$.pipe(
      ofType(DeliveryLogPageActions.detailOpened),
      switchMap(({ id }) =>
        api.get(id).pipe(
          map((detail) => DeliveryLogApiActions.detailSucceeded({ detail })),
          catchError((error: ApiError) => of(DeliveryLogApiActions.detailFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The button waits while a resend is on its way, and the key makes a repeat harmless on the server anyway (DLG-8). */
export const resend$ = createEffect(
  (actions$ = inject(Actions), api = inject(DeliveryLogApiService)) =>
    actions$.pipe(
      ofType(DeliveryLogPageActions.resendConfirmed),
      concatMap(({ id, key, acknowledgeDuplicate }) =>
        api.resend(id, key, acknowledgeDuplicate).pipe(
          map((result) => DeliveryLogApiActions.resendSucceeded({ id: result.id })),
          catchError((error: ApiError) => of(DeliveryLogApiActions.resendFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** After a resend the new delivery is opened (so its status can be watched), and the list and strip are reloaded. */
export const refreshAfterResend$ = createEffect(
  (actions$ = inject(Actions)) =>
    actions$.pipe(
      ofType(DeliveryLogApiActions.resendSucceeded),
      concatMap(({ id }) => [DeliveryLogPageActions.refreshed(), DeliveryLogPageActions.detailOpened({ id })]),
    ),
  { functional: true },
);
