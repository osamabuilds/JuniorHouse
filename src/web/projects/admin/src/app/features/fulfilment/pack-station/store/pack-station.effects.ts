import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, concatMap, filter, map, of, retry, switchMap, throwError, timer, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { PackStationApiService } from '../services/pack-station-api.service';
import { PackQueuePageActions, PackStationApiActions, PackStationPageActions } from './pack-station.actions';
import { selectQueueQuery, selectView } from './pack-station.selectors';

export const loadQueue$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PackStationApiService)) =>
    actions$.pipe(
      ofType(PackQueuePageActions.opened, PackQueuePageActions.pageChanged, PackQueuePageActions.pageSizeChanged, PackQueuePageActions.refreshed),
      withLatestFrom(store.select(selectQueueQuery)),
      switchMap(([, q]) =>
        api.queue(q.page, q.pageSize).pipe(
          map((result) => PackStationApiActions.queueSucceeded({ result })),
          catchError((error: ApiError) => of(PackStationApiActions.queueFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Opening a picked order takes the parcel for this packer. A refusal (not picked yet, or someone else has it) still shows the order, read-only. */
export const open$ = createEffect(
  (actions$ = inject(Actions), api = inject(PackStationApiService)) =>
    actions$.pipe(
      ofType(PackStationPageActions.opened),
      switchMap(({ orderId }) =>
        api.open(orderId).pipe(
          map((view) => PackStationApiActions.openSucceeded({ view })),
          catchError((error: ApiError) => of(PackStationApiActions.openRefused({ error, orderId }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadReadOnly$ = createEffect(
  (actions$ = inject(Actions), api = inject(PackStationApiService)) =>
    actions$.pipe(
      ofType(PackStationApiActions.openRefused),
      switchMap(({ orderId }) => loadView(api, orderId)),
    ),
  { functional: true },
);

/** After a scan, a weighing or sealing, the view is read again so the screen shows the truth. */
export const reloadView$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PackStationApiService)) =>
    actions$.pipe(
      ofType(PackStationApiActions.scanSucceeded, PackStationApiActions.weightSucceeded, PackStationApiActions.completeSucceeded, PackStationApiActions.scanFailed),
      withLatestFrom(store.select(selectView)),
      filter(([, view]) => view !== null),
      switchMap(([, view]) => loadView(api, view!.orderId)),
    ),
  { functional: true },
);

/** One small request per scan (APP-6). Only a server that could not be reached is tried again, so a unit is never counted twice. */
export const scan$ = createEffect(
  (actions$ = inject(Actions), api = inject(PackStationApiService)) =>
    actions$.pipe(
      ofType(PackStationPageActions.scanned),
      concatMap(({ orderId, code }) =>
        api.scan(orderId, code).pipe(
          retry({ count: 1, delay: (error: ApiError) => ([502, 503, 504].includes(error.status) ? timer(300) : throwError(() => error)) }),
          map((outcome) => PackStationApiActions.scanSucceeded({ outcome })),
          catchError((error: ApiError) => of(PackStationApiActions.scanFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const weigh$ = createEffect(
  (actions$ = inject(Actions), api = inject(PackStationApiService)) =>
    actions$.pipe(
      ofType(PackStationPageActions.weightSubmitted),
      concatMap(({ orderId, grams }) =>
        api.weigh(orderId, grams).pipe(
          map((outcome) => PackStationApiActions.weightSucceeded({ outcome })),
          catchError((error: ApiError) => of(PackStationApiActions.weightFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const complete$ = createEffect(
  (actions$ = inject(Actions), api = inject(PackStationApiService)) =>
    actions$.pipe(
      ofType(PackStationPageActions.completeClicked),
      concatMap(({ orderId, note }) =>
        api.complete(orderId, note).pipe(
          map((result) => PackStationApiActions.completeSucceeded({ result, orderId })),
          catchError((error: ApiError) => of(PackStationApiActions.completeFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

function loadView(api: PackStationApiService, orderId: number) {
  return api.get(orderId).pipe(
    map((view) => PackStationApiActions.viewSucceeded({ view })),
    catchError((error: ApiError) => of(PackStationApiActions.viewFailed({ error }))),
  );
}
