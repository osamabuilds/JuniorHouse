import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { LabelWorkApiService } from '../services/label-work-api.service';
import { LabelWorkApiActions, LabelWorkPageActions } from './label-work.actions';
import { selectFilters } from './label-work.selectors';

/** Re-runs the worklist search whenever a filter changes or an action changed what the list shows. */
export const loadItems$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(LabelWorkApiService)) =>
    actions$.pipe(
      ofType(
        LabelWorkPageActions.opened,
        LabelWorkPageActions.statusChanged,
        LabelWorkPageActions.priceFlagToggled,
        LabelWorkPageActions.pageChanged,
        LabelWorkPageActions.pageSizeChanged,
        LabelWorkPageActions.refreshRequested,
        LabelWorkApiActions.queueSucceeded,
        LabelWorkApiActions.resolveSucceeded,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => LabelWorkApiActions.loadItemsSucceeded({ result })),
          catchError((error: ApiError) => of(LabelWorkApiActions.loadItemsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadPrinters$ = createEffect(
  (actions$ = inject(Actions), api = inject(LabelWorkApiService)) =>
    actions$.pipe(
      ofType(LabelWorkPageActions.opened),
      switchMap(() =>
        api.listPrinters().pipe(
          map((printers) => LabelWorkApiActions.loadPrintersSucceeded({ printers })),
          catchError((error: ApiError) => of(LabelWorkApiActions.loadPrintersFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const queueTags$ = createEffect(
  (actions$ = inject(Actions), api = inject(LabelWorkApiService)) =>
    actions$.pipe(
      ofType(LabelWorkPageActions.queueSubmitted),
      switchMap(({ itemId, value }) =>
        api.queueTags(itemId, value).pipe(
          map((job) => LabelWorkApiActions.queueSucceeded({ job })),
          catchError((error: ApiError) => of(LabelWorkApiActions.queueFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const resolveItem$ = createEffect(
  (actions$ = inject(Actions), api = inject(LabelWorkApiService)) =>
    actions$.pipe(
      ofType(LabelWorkPageActions.resolveSubmitted),
      switchMap(({ itemId, value }) =>
        api.resolve(itemId, value).pipe(
          map((item) => LabelWorkApiActions.resolveSucceeded({ item })),
          catchError((error: ApiError) => of(LabelWorkApiActions.resolveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
