import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, tap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { AdjustmentApiService } from '@features/warehouse/adjustments';
import { CycleCountApiService } from '../services/cycle-count-api.service';
import { CycleCountsApiActions, CycleCountsPageActions } from './cycle-counts.actions';
import { selectBinZone, selectBinsPage, selectCountsPage, selectPageSize, selectSelectedBinIds, selectStatusFilter } from './cycle-counts.selectors';

export const loadCounts$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.listOpened, CycleCountsPageActions.statusFilterChanged, CycleCountsPageActions.countsPageChanged, CycleCountsApiActions.createFailed),
      withLatestFrom(store.select(selectStatusFilter), store.select(selectCountsPage), store.select(selectPageSize)),
      switchMap(([, status, page, pageSize]) =>
        api.list(status === 'all' ? null : status, page, pageSize).pipe(
          map((result) => CycleCountsApiActions.loadCountsSucceeded({ result })),
          catchError((error: ApiError) => of(CycleCountsApiActions.loadCountsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The bins to choose from, never-counted first (CC-11, decision D9). */
export const loadBins$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.listOpened, CycleCountsPageActions.binsPageChanged, CycleCountsPageActions.binZoneChanged),
      withLatestFrom(store.select(selectBinZone), store.select(selectBinsPage), store.select(selectPageSize)),
      switchMap(([, zone, page, pageSize]) =>
        api.binAge(zone, page, pageSize).pipe(
          map((result) => CycleCountsApiActions.loadBinsSucceeded({ result })),
          catchError((error: ApiError) => of(CycleCountsApiActions.loadBinsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Counted variances need a reason from the same list adjustments use (the reasons are a lookup, D12). */
export const loadReasons$ = createEffect(
  (actions$ = inject(Actions), adjustments = inject(AdjustmentApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.detailOpened),
      switchMap(() =>
        adjustments.reasons().pipe(
          map((reasons) => CycleCountsApiActions.loadReasonsSucceeded({ reasons })),
          catchError(() => of(CycleCountsApiActions.loadReasonsSucceeded({ reasons: [] }))),
        ),
      ),
    ),
  { functional: true },
);

export const create$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.createSubmitted),
      withLatestFrom(store.select(selectSelectedBinIds)),
      switchMap(([{ actingPerson }, binIds]) =>
        api.create(binIds, actingPerson).pipe(
          map((count) => CycleCountsApiActions.createSucceeded({ count })),
          catchError((error: ApiError) => of(CycleCountsApiActions.createFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** After a count is created the person goes straight to its count sheet. */
export const openCreated$ = createEffect(
  (actions$ = inject(Actions), router = inject(Router)) =>
    actions$.pipe(
      ofType(CycleCountsApiActions.createSucceeded),
      tap(({ count }) => void router.navigate(['/warehouse/cycle-counts', count.id])),
    ),
  { functional: true, dispatch: false },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.detailOpened),
      switchMap(({ id }) =>
        api.get(id).pipe(
          map((count) => CycleCountsApiActions.loadDetailSucceeded({ count })),
          catchError((error: ApiError) => of(CycleCountsApiActions.loadDetailFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const submitLines$ = createEffect(
  (actions$ = inject(Actions), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.linesSubmitted),
      switchMap(({ id, value }) =>
        api.submitLines(id, value).pipe(
          map((count) => CycleCountsApiActions.submitLinesSucceeded({ count })),
          catchError((error: ApiError) => of(CycleCountsApiActions.submitLinesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The review is the only place a system quantity appears, and only after counts are in (CC-2, CC-4). */
export const loadReview$ = createEffect(
  (actions$ = inject(Actions), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.reviewRequested),
      switchMap(({ id }) =>
        api.review(id).pipe(
          map((review) => CycleCountsApiActions.loadReviewSucceeded({ review })),
          catchError((error: ApiError) => of(CycleCountsApiActions.loadReviewFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const confirm$ = createEffect(
  (actions$ = inject(Actions), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.confirmSubmitted),
      switchMap(({ id, reasons, actingPerson }) =>
        api.confirm(id, reasons, actingPerson).pipe(
          map((count) => CycleCountsApiActions.confirmSucceeded({ count })),
          catchError((error: ApiError) => of(CycleCountsApiActions.confirmFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const cancel$ = createEffect(
  (actions$ = inject(Actions), api = inject(CycleCountApiService)) =>
    actions$.pipe(
      ofType(CycleCountsPageActions.cancelSubmitted),
      switchMap(({ id, actingPerson }) =>
        api.cancel(id, actingPerson).pipe(
          map((count) => CycleCountsApiActions.cancelSucceeded({ count })),
          catchError((error: ApiError) => of(CycleCountsApiActions.cancelFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
