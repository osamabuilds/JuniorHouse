import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { Observable, catchError, filter, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { ProductionApiService } from '../services/production-api.service';
import { ProductionApiActions, ProductionPageActions, ProductionRunActions } from './production.actions';
import { selectRunFilters, selectRunId } from './production.selectors';

type ActionResult = ReturnType<typeof ProductionApiActions.actionSucceeded> | ReturnType<typeof ProductionApiActions.actionFailed>;

/** Re-runs the search whenever a filter changes. */
export const loadRuns$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(
        ProductionPageActions.listOpened,
        ProductionPageActions.searchChanged,
        ProductionPageActions.statusFilterChanged,
        ProductionPageActions.atRiskToggled,
        ProductionPageActions.pageChanged,
        ProductionPageActions.pageSizeChanged,
      ),
      withLatestFrom(store.select(selectRunFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => ProductionApiActions.loadRunsSucceeded({ result })),
          catchError((error: ApiError) => of(ProductionApiActions.loadRunsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Loads the run when its page opens, and again after every successful action: the API judged the change, so show its verdict. */
export const loadRun$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.opened, ProductionApiActions.actionSucceeded, ProductionApiActions.fileUploadSucceeded),
      withLatestFrom(store.select(selectRunId)),
      map(([action, runId]) => ('id' in action ? action.id : runId)),
      filter((id): id is number => id !== null),
      switchMap((id) =>
        api.getById(id).pipe(
          map((run) => ProductionApiActions.loadRunSucceeded({ run })),
          catchError((error: ApiError) => of(ProductionApiActions.loadRunFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

function act<T>(call: Observable<T>, notice: string): Observable<ActionResult> {
  return call.pipe(
    map(() => ProductionApiActions.actionSucceeded({ notice })),
    catchError((error: ApiError) => of(ProductionApiActions.actionFailed({ error }))),
  );
}

export const recordMilestone$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.milestoneSubmitted),
      withLatestFrom(store.select(selectRunId)),
      filter(([, runId]) => runId !== null),
      switchMap(([{ value }, runId]) => act(api.recordMilestone(runId!, value), 'Milestone recorded.')),
    ),
  { functional: true },
);

export const submitSample$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.sampleSubmitted),
      withLatestFrom(store.select(selectRunId)),
      filter(([, runId]) => runId !== null),
      switchMap(([{ value }, runId]) => act(api.submitSample(runId!, value), 'Sample recorded as submitted.')),
    ),
  { functional: true },
);

export const decideSample$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.sampleDecided),
      withLatestFrom(store.select(selectRunId)),
      filter(([, runId]) => runId !== null),
      switchMap(([{ value }, runId]) =>
        act(
          api.decideSample(runId!, value),
          value.approve ? 'Sample approved. Bulk cutting can now be recorded.' : 'Sample rejected. The vendor can send another.',
        ),
      ),
    ),
  { functional: true },
);

export const setExpectedCompletion$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.expectedCompletionSubmitted),
      withLatestFrom(store.select(selectRunId)),
      filter(([, runId]) => runId !== null),
      switchMap(([{ value }, runId]) => act(api.setExpectedCompletion(runId!, value), 'Expected completion date saved.')),
    ),
  { functional: true },
);

export const recordDeliveryNote$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.deliveryNoteSubmitted),
      withLatestFrom(store.select(selectRunId)),
      filter(([, runId]) => runId !== null),
      switchMap(([{ value }, runId]) => act(api.recordDeliveryNote(runId!, value), 'Delivery note recorded.')),
    ),
  { functional: true },
);

export const uploadSampleFile$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.sampleFileUploadRequested),
      withLatestFrom(store.select(selectRunId)),
      filter(([, runId]) => runId !== null),
      switchMap(([{ upload }, runId]) =>
        api.uploadSampleFile(runId!, upload.roundNo, upload.file).pipe(
          map(() => ProductionApiActions.fileUploadSucceeded()),
          catchError((error: ApiError) => of(ProductionApiActions.fileUploadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const removeSampleFile$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ProductionApiService)) =>
    actions$.pipe(
      ofType(ProductionRunActions.sampleFileRemoveRequested),
      withLatestFrom(store.select(selectRunId)),
      filter(([, runId]) => runId !== null),
      switchMap(([{ roundNo, fileId }, runId]) =>
        api.removeSampleFile(runId!, roundNo, fileId).pipe(
          map(() => ProductionApiActions.fileUploadSucceeded()),
          catchError((error: ApiError) => of(ProductionApiActions.fileUploadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
