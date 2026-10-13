import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { Observable, catchError, filter, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { QualityApiService } from '../services/quality-api.service';
import { BatchListActions, BatchPageActions, QualityApiActions, ReceivePageActions, ReceivingQueueActions } from './quality.actions';
import { selectBatchFilters, selectBatchId, selectDeliveryFilters } from './quality.selectors';

type ActionResult = ReturnType<typeof QualityApiActions.actionSucceeded> | ReturnType<typeof QualityApiActions.actionFailed>;

/** Re-runs the receiving queue search whenever a filter changes. */
export const loadDeliveries$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(
        ReceivingQueueActions.opened,
        ReceivingQueueActions.searchChanged,
        ReceivingQueueActions.statusFilterChanged,
        ReceivingQueueActions.pageChanged,
        ReceivingQueueActions.pageSizeChanged,
      ),
      withLatestFrom(store.select(selectDeliveryFilters)),
      switchMap(([, filters]) =>
        api.searchDeliveries(filters).pipe(
          map((result) => QualityApiActions.loadDeliveriesSucceeded({ result })),
          catchError((error: ApiError) => of(QualityApiActions.loadDeliveriesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadDelivery$ = createEffect(
  (actions$ = inject(Actions), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(ReceivePageActions.opened),
      switchMap(({ id }) =>
        api.getDelivery(id).pipe(
          map((delivery) => QualityApiActions.loadDeliverySucceeded({ delivery })),
          catchError((error: ApiError) => of(QualityApiActions.loadDeliveryFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const recordReceipt$ = createEffect(
  (actions$ = inject(Actions), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(ReceivePageActions.receiptSubmitted),
      switchMap(({ value }) =>
        api.recordReceipt(value).pipe(
          map((receipt) => QualityApiActions.receiptSucceeded({ receipt })),
          catchError((error: ApiError) => of(QualityApiActions.receiptFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadBatches$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(
        BatchListActions.opened,
        BatchListActions.searchChanged,
        BatchListActions.statusFilterChanged,
        BatchListActions.pageChanged,
        BatchListActions.pageSizeChanged,
      ),
      withLatestFrom(store.select(selectBatchFilters)),
      switchMap(([, filters]) =>
        api.searchBatches(filters).pipe(
          map((result) => QualityApiActions.loadBatchesSucceeded({ result })),
          catchError((error: ApiError) => of(QualityApiActions.loadBatchesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Loads the batch when its page opens, and again after every successful action: the API judged the change, so show its verdict. */
export const loadBatch$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(BatchPageActions.opened, QualityApiActions.actionSucceeded),
      withLatestFrom(store.select(selectBatchId)),
      map(([action, batchId]) => ('id' in action ? action.id : batchId)),
      filter((id): id is number => id !== null),
      switchMap((id) =>
        api.getBatch(id).pipe(
          map((batch) => QualityApiActions.loadBatchSucceeded({ batch })),
          catchError((error: ApiError) => of(QualityApiActions.loadBatchFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The AQL plan the inspection form previews: fetched for the scope's lot size. */
export const loadPlan$ = createEffect(
  (actions$ = inject(Actions), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(BatchPageActions.planRequested),
      switchMap(({ lotQty }) =>
        api.previewPlan(lotQty).pipe(
          map((plan) => QualityApiActions.planLoaded({ plan })),
          catchError((error: ApiError) => of(QualityApiActions.planFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

function act<T>(call: Observable<T>, notice: string): Observable<ActionResult> {
  return call.pipe(
    map(() => QualityApiActions.actionSucceeded({ notice })),
    catchError((error: ApiError) => of(QualityApiActions.actionFailed({ error }))),
  );
}

export const recordInspection$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(BatchPageActions.inspectionSubmitted),
      withLatestFrom(store.select(selectBatchId)),
      filter(([, batchId]) => batchId !== null),
      switchMap(([{ value }, batchId]) => act(api.recordInspection(batchId!, value), 'Inspection recorded.')),
    ),
  { functional: true },
);

export const recordSafetyChecks$ = createEffect(
  (actions$ = inject(Actions), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(BatchPageActions.safetyChecksSubmitted),
      switchMap(({ value }) => act(api.recordSafetyChecks(value), 'Child-safety checks recorded.')),
    ),
  { functional: true },
);

export const decideBatch$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(BatchPageActions.decisionSubmitted),
      withLatestFrom(store.select(selectBatchId)),
      filter(([, batchId]) => batchId !== null),
      switchMap(([{ value }, batchId]) => act(api.decideBatch(batchId!, value), 'Outcome recorded. Accepted quantity is released.')),
    ),
  { functional: true },
);

export const decidePart$ = createEffect(
  (actions$ = inject(Actions), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(BatchPageActions.partDecisionSubmitted),
      switchMap(({ value }) => act(api.decideSubBatch(value), 'Outcome recorded for the returned part.')),
    ),
  { functional: true },
);

export const recordReworkReturn$ = createEffect(
  (actions$ = inject(Actions), api = inject(QualityApiService)) =>
    actions$.pipe(
      ofType(BatchPageActions.reworkReturnSubmitted),
      switchMap(({ value }) => act(api.recordReworkReturn(value), 'Return recorded. The returned part now needs a re-inspection.')),
    ),
  { functional: true },
);
