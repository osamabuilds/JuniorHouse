import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { PUTAWAY_PAGE_SIZE } from '../putaway.constants';
import { PutawayApiService } from '../services/putaway-api.service';
import { PutawayApiActions, PutawayPageActions } from './putaway.actions';
import { selectItemSearch, selectItemsPage, selectRecordsPage } from './putaway.selectors';

/** Reloads the queue when the page opens, changes page, is refreshed, or a putaway changed what is waiting. */
export const loadItems$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PutawayApiService)) =>
    actions$.pipe(
      ofType(
        PutawayPageActions.opened,
        PutawayPageActions.itemsPageChanged,
        PutawayPageActions.itemSearchChanged,
        PutawayPageActions.refreshRequested,
        PutawayApiActions.confirmSucceeded,
      ),
      withLatestFrom(store.select(selectItemSearch), store.select(selectItemsPage)),
      switchMap(([, search, page]) =>
        api.items(search, page, PUTAWAY_PAGE_SIZE).pipe(
          map((result) => PutawayApiActions.loadItemsSucceeded({ result })),
          catchError((error: ApiError) => of(PutawayApiActions.loadItemsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadRecords$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PutawayApiService)) =>
    actions$.pipe(
      ofType(PutawayPageActions.opened, PutawayPageActions.recordsPageChanged, PutawayPageActions.refreshRequested, PutawayApiActions.confirmSucceeded),
      withLatestFrom(store.select(selectRecordsPage)),
      switchMap(([, page]) =>
        api.records(page, PUTAWAY_PAGE_SIZE).pipe(
          map((result) => PutawayApiActions.loadRecordsSucceeded({ result })),
          catchError((error: ApiError) => of(PutawayApiActions.loadRecordsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** One small GET per scan (NFR-SC-01, PUT-18); a newer scan cancels an older one still in flight. */
export const scan$ = createEffect(
  (actions$ = inject(Actions), api = inject(PutawayApiService)) =>
    actions$.pipe(
      ofType(PutawayPageActions.scanSubmitted),
      switchMap(({ gtin }) =>
        api.scan(gtin).pipe(
          map((result) => PutawayApiActions.scanSucceeded({ result })),
          catchError((error: ApiError) => of(PutawayApiActions.scanFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const confirm$ = createEffect(
  (actions$ = inject(Actions), api = inject(PutawayApiService)) =>
    actions$.pipe(
      ofType(PutawayPageActions.confirmSubmitted),
      switchMap(({ itemId, value }) =>
        api.confirm(itemId, value).pipe(
          map((record) => PutawayApiActions.confirmSucceeded({ record })),
          catchError((error: ApiError) => of(PutawayApiActions.confirmFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
