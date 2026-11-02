import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { ShippingApiService } from '../services/shipping-api.service';
import { ShippingApiActions, ShippingPageActions } from './shipping.actions';
import { selectPaging } from './shipping.selectors';

/** Re-runs the list when the page opens, the paging changes, or a rate was added or removed. */
export const loadRates$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ShippingApiService)) =>
    actions$.pipe(
      ofType(
        ShippingPageActions.opened,
        ShippingPageActions.pageChanged,
        ShippingPageActions.pageSizeChanged,
        ShippingApiActions.saveRateSucceeded,
        ShippingApiActions.removeRateSucceeded,
      ),
      withLatestFrom(store.select(selectPaging)),
      switchMap(([, paging]) =>
        api.rates(paging.page, paging.pageSize).pipe(
          map((result) => ShippingApiActions.loadRatesSucceeded({ result })),
          catchError((error: ApiError) => of(ShippingApiActions.loadRatesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadSettings$ = createEffect(
  (actions$ = inject(Actions), api = inject(ShippingApiService)) =>
    actions$.pipe(
      ofType(ShippingPageActions.opened),
      switchMap(() =>
        api.settings().pipe(
          map((settings) => ShippingApiActions.loadSettingsSucceeded({ settings })),
          catchError((error: ApiError) => of(ShippingApiActions.loadSettingsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadProvinces$ = createEffect(
  (actions$ = inject(Actions), api = inject(ShippingApiService)) =>
    actions$.pipe(
      ofType(ShippingPageActions.opened),
      switchMap(() =>
        api.provinces().pipe(
          map((provinces) => ShippingApiActions.loadProvincesSucceeded({ provinces })),
          catchError(() => of(ShippingApiActions.loadProvincesSucceeded({ provinces: [] }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadCities$ = createEffect(
  (actions$ = inject(Actions), api = inject(ShippingApiService)) =>
    actions$.pipe(
      ofType(ShippingPageActions.provinceChosen),
      switchMap(({ provinceId }) =>
        api.cities(provinceId).pipe(
          map((cities) => ShippingApiActions.loadCitiesSucceeded({ cities })),
          catchError(() => of(ShippingApiActions.loadCitiesSucceeded({ cities: [] }))),
        ),
      ),
    ),
  { functional: true },
);

export const saveSettings$ = createEffect(
  (actions$ = inject(Actions), api = inject(ShippingApiService)) =>
    actions$.pipe(
      ofType(ShippingPageActions.settingsSubmitted),
      switchMap(({ value }) =>
        api.saveSettings(value).pipe(
          map((settings) => ShippingApiActions.saveSettingsSucceeded({ settings })),
          catchError((error: ApiError) => of(ShippingApiActions.saveSettingsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const saveRate$ = createEffect(
  (actions$ = inject(Actions), api = inject(ShippingApiService)) =>
    actions$.pipe(
      ofType(ShippingPageActions.rateSubmitted),
      switchMap(({ cityId, fee }) =>
        api.setRate(cityId, fee).pipe(
          map((rate) => ShippingApiActions.saveRateSucceeded({ rate })),
          catchError((error: ApiError) => of(ShippingApiActions.saveRateFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const removeRate$ = createEffect(
  (actions$ = inject(Actions), api = inject(ShippingApiService)) =>
    actions$.pipe(
      ofType(ShippingPageActions.rateRemoved),
      switchMap(({ cityId }) =>
        api.removeRate(cityId).pipe(
          map(() => ShippingApiActions.removeRateSucceeded({ cityId })),
          catchError((error: ApiError) => of(ShippingApiActions.removeRateFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
