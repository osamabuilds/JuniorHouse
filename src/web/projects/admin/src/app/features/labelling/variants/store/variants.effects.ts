import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { VariantApiService } from '../services/variant-api.service';
import { VariantsApiActions, VariantsPageActions } from './variants.actions';
import { selectFilters } from './variants.selectors';

/** Re-runs the register search whenever a filter changes or a void changed what the list shows. */
export const loadVariants$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(VariantApiService)) =>
    actions$.pipe(
      ofType(
        VariantsPageActions.opened,
        VariantsPageActions.searchChanged,
        VariantsPageActions.statusChanged,
        VariantsPageActions.pageChanged,
        VariantsPageActions.pageSizeChanged,
        VariantsPageActions.refreshRequested,
        VariantsApiActions.voidSucceeded,
      ),
      withLatestFrom(store.select(selectFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => VariantsApiActions.loadVariantsSucceeded({ result })),
          catchError((error: ApiError) => of(VariantsApiActions.loadVariantsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** The capacity banner is refreshed on open and after a void (a void permanently uses up a number, VAR-12). */
export const loadCapacity$ = createEffect(
  (actions$ = inject(Actions), api = inject(VariantApiService)) =>
    actions$.pipe(
      ofType(VariantsPageActions.opened, VariantsApiActions.voidSucceeded),
      switchMap(() =>
        api.getCapacity().pipe(
          map((capacity) => VariantsApiActions.loadCapacitySucceeded({ capacity })),
          catchError((error: ApiError) => of(VariantsApiActions.loadCapacityFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(VariantApiService)) =>
    actions$.pipe(
      ofType(VariantsPageActions.detailRequested),
      switchMap(({ id }) =>
        api.getById(id).pipe(
          map((detail) => VariantsApiActions.loadDetailSucceeded({ detail })),
          catchError((error: ApiError) => of(VariantsApiActions.loadDetailFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const voidGtin$ = createEffect(
  (actions$ = inject(Actions), api = inject(VariantApiService)) =>
    actions$.pipe(
      ofType(VariantsPageActions.voidSubmitted),
      switchMap(({ id, value }) =>
        api.voidGtin(id, value).pipe(
          map((detail) => VariantsApiActions.voidSucceeded({ detail })),
          catchError((error: ApiError) => of(VariantsApiActions.voidFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
