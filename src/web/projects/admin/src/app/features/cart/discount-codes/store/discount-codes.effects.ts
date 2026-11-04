import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { DiscountCodesApiService } from '../services/discount-codes-api.service';
import { DiscountCodesApiActions, DiscountCodesPageActions } from './discount-codes.actions';
import { selectEditingId, selectPaging } from './discount-codes.selectors';

/** Re-runs the list when the page opens, the paging changes, or a code was saved. */
export const loadCodes$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(DiscountCodesApiService)) =>
    actions$.pipe(
      ofType(
        DiscountCodesPageActions.opened,
        DiscountCodesPageActions.pageChanged,
        DiscountCodesPageActions.pageSizeChanged,
        DiscountCodesApiActions.saveSucceeded,
      ),
      withLatestFrom(store.select(selectPaging)),
      switchMap(([, paging]) =>
        api.list(paging.page, paging.pageSize).pipe(
          map((result) => DiscountCodesApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(DiscountCodesApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** A new code when nothing is being edited, otherwise a change to the one being edited. */
export const save$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(DiscountCodesApiService)) =>
    actions$.pipe(
      ofType(DiscountCodesPageActions.submitted),
      withLatestFrom(store.select(selectEditingId)),
      switchMap(([{ input }, editingId]) =>
        (editingId === null ? api.create(input) : api.update(editingId, input)).pipe(
          map((code) => DiscountCodesApiActions.saveSucceeded({ code, created: editingId === null })),
          catchError((error: ApiError) => of(DiscountCodesApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const switchActive$ = createEffect(
  (actions$ = inject(Actions), api = inject(DiscountCodesApiService)) =>
    actions$.pipe(
      ofType(DiscountCodesPageActions.switched),
      switchMap(({ id, active }) =>
        api.setActive(id, active).pipe(
          map((code) => DiscountCodesApiActions.switchSucceeded({ code })),
          catchError((error: ApiError) => of(DiscountCodesApiActions.switchFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
