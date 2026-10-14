import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, filter, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { StockApiService } from '../services/stock-api.service';
import { StockApiActions, StockPageActions } from './stock.actions';
import { selectFilters, selectHistoryFor, selectMovementsPage, selectPageSize } from './stock.selectors';

/** Re-runs the stock list whenever a filter changes. */
export const loadStock$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StockApiService)) =>
    actions$.pipe(
      ofType(
        StockPageActions.opened,
        StockPageActions.searchChanged,
        StockPageActions.pageChanged,
        StockPageActions.pageSizeChanged,
        StockPageActions.refreshRequested,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => StockApiActions.loadStockSucceeded({ result })),
          catchError((error: ApiError) => of(StockApiActions.loadStockFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The movement history of the variant that is open, newest first (LED-9). */
export const loadMovements$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StockApiService)) =>
    actions$.pipe(
      ofType(StockPageActions.historyRequested, StockPageActions.historyPageChanged),
      withLatestFrom(store.select(selectHistoryFor), store.select(selectMovementsPage), store.select(selectPageSize)),
      filter(([, variant]) => variant !== null),
      switchMap(([, variant, page, pageSize]) =>
        api.movements(variant!.variantRefId, page, pageSize).pipe(
          map((result) => StockApiActions.loadMovementsSucceeded({ result })),
          catchError((error: ApiError) => of(StockApiActions.loadMovementsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
