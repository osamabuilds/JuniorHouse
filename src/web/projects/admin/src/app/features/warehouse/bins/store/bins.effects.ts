import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { BinApiService } from '../services/bin-api.service';
import { BinsApiActions, BinsPageActions } from './bins.actions';
import { selectFilters } from './bins.selectors';

/** Re-runs the list whenever a filter changes or an add changed what the list shows. */
export const loadBins$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(BinApiService)) =>
    actions$.pipe(
      ofType(
        BinsPageActions.opened,
        BinsPageActions.zoneChanged,
        BinsPageActions.pageChanged,
        BinsPageActions.pageSizeChanged,
        BinsPageActions.refreshRequested,
        BinsApiActions.createSucceeded,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => BinsApiActions.loadBinsSucceeded({ result })),
          catchError((error: ApiError) => of(BinsApiActions.loadBinsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const createBin$ = createEffect(
  (actions$ = inject(Actions), api = inject(BinApiService)) =>
    actions$.pipe(
      ofType(BinsPageActions.createSubmitted),
      switchMap(({ value }) =>
        api.create(value).pipe(
          map((bin) => BinsApiActions.createSucceeded({ bin })),
          catchError((error: ApiError) => of(BinsApiActions.createFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const toggleActive$ = createEffect(
  (actions$ = inject(Actions), api = inject(BinApiService)) =>
    actions$.pipe(
      ofType(BinsPageActions.activeToggled),
      switchMap(({ bin, actingPerson }) =>
        api.setActive(bin.id, !bin.isActive, actingPerson).pipe(
          map((updated) => BinsApiActions.toggleSucceeded({ bin: updated })),
          catchError((error: ApiError) => of(BinsApiActions.toggleFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
