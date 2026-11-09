import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, concatMap, filter, map, of, retry, switchMap, throwError, timer, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { ExceptionsApiService } from '../../pick-exceptions';
import { PickListsApiService } from '../services/pick-lists-api.service';
import { PickListsApiActions, PickQueuePageActions, PickStationPageActions } from './pick-lists.actions';
import { selectList, selectQueueQuery } from './pick-lists.selectors';

/** Re-runs the queue when the page opens, the paging changes or a refresh asks for it. */
export const loadQueue$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PickListsApiService)) =>
    actions$.pipe(
      ofType(PickQueuePageActions.opened, PickQueuePageActions.pageChanged, PickQueuePageActions.pageSizeChanged, PickQueuePageActions.refreshed),
      withLatestFrom(store.select(selectQueueQuery)),
      switchMap(([, q]) =>
        api.queue(q.page, q.pageSize).pipe(
          map((result) => PickListsApiActions.queueSucceeded({ result })),
          catchError((error: ApiError) => of(PickListsApiActions.queueFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Opening an order takes the pick for this picker (PCK-9). A refusal still shows the list, read-only. */
export const claim$ = createEffect(
  (actions$ = inject(Actions), api = inject(PickListsApiService)) =>
    actions$.pipe(
      ofType(PickStationPageActions.opened),
      switchMap(({ orderId }) =>
        api.claim(orderId).pipe(
          map((list) => PickListsApiActions.claimSucceeded({ list })),
          catchError((error: ApiError) => of(PickListsApiActions.claimRefused({ error, orderId }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadReadOnly$ = createEffect(
  (actions$ = inject(Actions), api = inject(PickListsApiService)) =>
    actions$.pipe(
      ofType(PickListsApiActions.claimRefused),
      switchMap(({ orderId }) => loadList(api, orderId)),
    ),
  { functional: true },
);

/**
 * After an accepted scan, a completion, a flagged line or a scan that got no answer, the list is read again, so the screen shows the truth. The
 * order is the one on screen.
 */
export const reloadList$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PickListsApiService)) =>
    actions$.pipe(
      ofType(PickListsApiActions.scanSucceeded, PickListsApiActions.completeSucceeded, PickListsApiActions.exceptionSucceeded, PickListsApiActions.scanFailed),
      filter((action) => action.type !== PickListsApiActions.scanSucceeded.type || (action as ReturnType<typeof PickListsApiActions.scanSucceeded>).outcome.accepted),
      withLatestFrom(store.select(selectList)),
      filter(([, list]) => list !== null),
      switchMap(([, list]) => loadList(api, list!.orderId)),
    ),
  { functional: true },
);

/**
 * One small request per scan (APP-6). A server that could not be reached (502, 503, 504) is tried once more; "no answer at all" is not retried,
 * because the scan may have been counted and a second one would count a unit twice: the screen says so and the list shows the truth.
 */
export const scan$ = createEffect(
  (actions$ = inject(Actions), api = inject(PickListsApiService)) =>
    actions$.pipe(
      ofType(PickStationPageActions.scanned),
      concatMap(({ orderId, code }) =>
        api.scan(orderId, code).pipe(
          retry({ count: 1, delay: (error: ApiError) => ([502, 503, 504].includes(error.status) ? timer(300) : throwError(() => error)) }),
          map((outcome) => PickListsApiActions.scanSucceeded({ outcome, orderId })),
          catchError((error: ApiError) => of(PickListsApiActions.scanFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const complete$ = createEffect(
  (actions$ = inject(Actions), api = inject(PickListsApiService)) =>
    actions$.pipe(
      ofType(PickStationPageActions.completeClicked),
      concatMap(({ orderId }) =>
        api.complete(orderId).pipe(
          map((result) => PickListsApiActions.completeSucceeded({ result, orderId })),
          catchError((error: ApiError) => of(PickListsApiActions.completeFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const release$ = createEffect(
  (actions$ = inject(Actions), api = inject(PickListsApiService)) =>
    actions$.pipe(
      ofType(PickStationPageActions.releaseClicked),
      concatMap(({ orderId }) =>
        api.release(orderId).pipe(
          map(() => PickListsApiActions.releaseSucceeded({ orderId })),
          catchError((error: ApiError) => of(PickListsApiActions.releaseFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The picker flags a line instead of force-completing (PEX-1). The exceptions feature owns the call; this store only reacts to it. */
export const raiseException$ = createEffect(
  (actions$ = inject(Actions), exceptions = inject(ExceptionsApiService)) =>
    actions$.pipe(
      ofType(PickStationPageActions.exceptionFlagged),
      concatMap(({ orderId, request }) =>
        exceptions.raise(orderId, request).pipe(
          map(() => PickListsApiActions.exceptionSucceeded({ orderId })),
          catchError((error: ApiError) => of(PickListsApiActions.exceptionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

function loadList(api: PickListsApiService, orderId: number) {
  return api.get(orderId).pipe(
    map((list) => PickListsApiActions.listSucceeded({ list })),
    catchError((error: ApiError) => of(PickListsApiActions.listFailed({ error }))),
  );
}
