import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { VendorApiService } from '../services/vendor-api.service';
import { VendorsApiActions, VendorsPageActions } from './vendors.actions';
import { selectVendorFilters } from './vendors.selectors';

/** Re-runs the vendor search whenever a filter changes or a save changes the list. */
export const loadVendors$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(VendorApiService)) =>
    actions$.pipe(
      ofType(
        VendorsPageActions.opened,
        VendorsPageActions.searchChanged,
        VendorsPageActions.specialisationFilterChanged,
        VendorsPageActions.activeOnlyToggled,
        VendorsApiActions.saveSucceeded,
      ),
      withLatestFrom(store.select(selectVendorFilters)),
      switchMap(([, filters]) =>
        api.search(filters.searchText, filters.specialisationId, filters.activeOnly).pipe(
          map((vendors) => VendorsApiActions.loadVendorsSucceeded({ vendors })),
          catchError((error: ApiError) => of(VendorsApiActions.loadVendorsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadVendor$ = createEffect(
  (actions$ = inject(Actions), api = inject(VendorApiService)) =>
    actions$.pipe(
      ofType(VendorsPageActions.editRequested),
      switchMap(({ id }) =>
        api.getById(id).pipe(
          map((vendor) => VendorsApiActions.loadVendorSucceeded({ vendor })),
          catchError((error: ApiError) => of(VendorsApiActions.loadVendorFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const createVendor$ = createEffect(
  (actions$ = inject(Actions), api = inject(VendorApiService)) =>
    actions$.pipe(
      ofType(VendorsPageActions.createSubmitted),
      switchMap(({ value }) =>
        api.create(value).pipe(
          map(() => VendorsApiActions.saveSucceeded()),
          catchError((error: ApiError) => of(VendorsApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const updateVendor$ = createEffect(
  (actions$ = inject(Actions), api = inject(VendorApiService)) =>
    actions$.pipe(
      ofType(VendorsPageActions.updateSubmitted),
      switchMap(({ id, value }) =>
        api.update(id, value).pipe(
          map(() => VendorsApiActions.saveSucceeded()),
          catchError((error: ApiError) => of(VendorsApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
