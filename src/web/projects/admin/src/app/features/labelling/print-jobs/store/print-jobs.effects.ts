import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { PrintJobApiService } from '../services/print-job-api.service';
import { PrintJobsApiActions, PrintJobsPageActions } from './print-jobs.actions';
import { selectFilters } from './print-jobs.selectors';

/** Re-runs the search whenever a filter changes, an action changed a job, or the screen's timer ticks. */
export const loadJobs$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PrintJobApiService)) =>
    actions$.pipe(
      ofType(
        PrintJobsPageActions.opened,
        PrintJobsPageActions.statusChanged,
        PrintJobsPageActions.pageChanged,
        PrintJobsPageActions.pageSizeChanged,
        PrintJobsPageActions.refreshRequested,
        PrintJobsPageActions.autoRefreshTicked,
        PrintJobsPageActions.historyCleared,
        PrintJobsApiActions.jobActionSucceeded,
        PrintJobsApiActions.reprintSucceeded,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => PrintJobsApiActions.loadJobsSucceeded({ result })),
          catchError((error: ApiError) => of(PrintJobsApiActions.loadJobsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadPrinters$ = createEffect(
  (actions$ = inject(Actions), api = inject(PrintJobApiService)) =>
    actions$.pipe(
      ofType(PrintJobsPageActions.opened),
      switchMap(() =>
        api.listPrinters().pipe(
          map((printers) => PrintJobsApiActions.loadPrintersSucceeded({ printers })),
          catchError((error: ApiError) => of(PrintJobsApiActions.loadPrintersFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const retryJob$ = createEffect(
  (actions$ = inject(Actions), api = inject(PrintJobApiService)) =>
    actions$.pipe(
      ofType(PrintJobsPageActions.retrySubmitted),
      switchMap(({ id, actingPerson }) =>
        api.retry(id, actingPerson).pipe(
          map((job) => PrintJobsApiActions.jobActionSucceeded({ job, verb: 'is back in the queue' })),
          catchError((error: ApiError) => of(PrintJobsApiActions.jobActionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const cancelJob$ = createEffect(
  (actions$ = inject(Actions), api = inject(PrintJobApiService)) =>
    actions$.pipe(
      ofType(PrintJobsPageActions.cancelSubmitted),
      switchMap(({ id, actingPerson }) =>
        api.cancel(id, actingPerson).pipe(
          map((job) => PrintJobsApiActions.jobActionSucceeded({ job, verb: 'was cancelled' })),
          catchError((error: ApiError) => of(PrintJobsApiActions.jobActionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const reprint$ = createEffect(
  (actions$ = inject(Actions), api = inject(PrintJobApiService)) =>
    actions$.pipe(
      ofType(PrintJobsPageActions.reprintSubmitted),
      switchMap(({ variantId, value }) =>
        api.reprint(variantId, value).pipe(
          map((job) => PrintJobsApiActions.reprintSucceeded({ job })),
          catchError((error: ApiError) => of(PrintJobsApiActions.reprintFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
