import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { SizeChartApiService } from '../services/size-chart-api.service';
import { SizeChartsApiActions, SizeChartsPageActions } from './size-charts.actions';
import { selectChart, selectFilters } from './size-charts.selectors';

/** Re-runs the list whenever the page changes or a save or delete changed what the list shows. */
export const loadList$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(SizeChartApiService)) =>
    actions$.pipe(
      ofType(
        SizeChartsPageActions.opened,
        SizeChartsPageActions.pageChanged,
        SizeChartsPageActions.pageSizeChanged,
        SizeChartsApiActions.saveSucceeded,
        SizeChartsApiActions.deleteSucceeded,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.list(filters).pipe(
          map((result) => SizeChartsApiActions.loadListSucceeded({ result })),
          catchError((error: ApiError) => of(SizeChartsApiActions.loadListFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadChart$ = createEffect(
  (actions$ = inject(Actions), api = inject(SizeChartApiService)) =>
    actions$.pipe(
      ofType(SizeChartsPageActions.editOpened),
      switchMap(({ id }) =>
        api.get(id).pipe(
          map((chart) => SizeChartsApiActions.loadChartSucceeded({ chart })),
          catchError((error: ApiError) => of(SizeChartsApiActions.loadChartFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const createChart$ = createEffect(
  (actions$ = inject(Actions), api = inject(SizeChartApiService)) =>
    actions$.pipe(
      ofType(SizeChartsPageActions.createSubmitted),
      switchMap(({ value, person }) =>
        api.create(value, person).pipe(
          map((chart) => SizeChartsApiActions.saveSucceeded({ chart })),
          catchError((error: ApiError) => of(SizeChartsApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const updateChart$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(SizeChartApiService)) =>
    actions$.pipe(
      ofType(SizeChartsPageActions.updateSubmitted),
      withLatestFrom(store.select(selectChart)),
      switchMap(([{ value, person }, chart]) =>
        api.update(chart!.id, value.name, value.lines, chart!.version, person).pipe(
          map((saved) => SizeChartsApiActions.saveSucceeded({ chart: saved })),
          catchError((error: ApiError) => of(SizeChartsApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const deleteChart$ = createEffect(
  (actions$ = inject(Actions), api = inject(SizeChartApiService)) =>
    actions$.pipe(
      ofType(SizeChartsPageActions.deleteConfirmed),
      switchMap(({ id, person }) =>
        api.delete(id, person).pipe(
          map(() => SizeChartsApiActions.deleteSucceeded()),
          catchError((error: ApiError) => of(SizeChartsApiActions.deleteFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
