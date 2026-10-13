import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { CatalogApiService } from '../services/catalog-api.service';
import { StylesApiActions, StylesPageActions } from './styles.actions';
import { selectStyleFilters } from './styles.selectors';

/** Re-runs the style search whenever a filter changes or a save changes the list. */
export const loadStyles$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CatalogApiService)) =>
    actions$.pipe(
      ofType(
        StylesPageActions.opened,
        StylesPageActions.searchChanged,
        StylesPageActions.categoryFilterChanged,
        StylesPageActions.activeOnlyToggled,
        StylesPageActions.pageChanged,
        StylesPageActions.pageSizeChanged,
        StylesApiActions.saveSucceeded,
      ),
      withLatestFrom(store.select(selectStyleFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => StylesApiActions.loadStylesSucceeded({ result })),
          catchError((error: ApiError) => of(StylesApiActions.loadStylesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadStyle$ = createEffect(
  (actions$ = inject(Actions), api = inject(CatalogApiService)) =>
    actions$.pipe(
      ofType(StylesPageActions.editRequested),
      switchMap(({ id }) =>
        api.getById(id).pipe(
          map((style) => StylesApiActions.loadStyleSucceeded({ style })),
          catchError((error: ApiError) => of(StylesApiActions.loadStyleFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const createStyle$ = createEffect(
  (actions$ = inject(Actions), api = inject(CatalogApiService)) =>
    actions$.pipe(
      ofType(StylesPageActions.createSubmitted),
      switchMap(({ value }) =>
        api.create(value).pipe(
          map(() => StylesApiActions.saveSucceeded()),
          catchError((error: ApiError) => of(StylesApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const updateStyle$ = createEffect(
  (actions$ = inject(Actions), api = inject(CatalogApiService)) =>
    actions$.pipe(
      ofType(StylesPageActions.updateSubmitted),
      switchMap(({ id, value }) =>
        api.update(id, value).pipe(
          map(() => StylesApiActions.saveSucceeded()),
          catchError((error: ApiError) => of(StylesApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
