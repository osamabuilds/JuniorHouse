import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { AlertApiService } from '../services/alert-api.service';
import { StockAlertsApiActions, StockAlertsPageActions } from './stock-alerts.actions';
import { selectAlertFilters, selectThresholdFilters } from './stock-alerts.selectors';

/** Reloads the alerts when a filter changes, and after a threshold change, which can open or close an alert at once (ALR-6). */
export const loadAlerts$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(AlertApiService)) =>
    actions$.pipe(
      ofType(
        StockAlertsPageActions.opened,
        StockAlertsPageActions.levelChanged,
        StockAlertsPageActions.unacknowledgedOnlyChanged,
        StockAlertsPageActions.alertsPageChanged,
        StockAlertsPageActions.refreshRequested,
        StockAlertsApiActions.thresholdSucceeded,
      ),
      withLatestFrom(store.select(selectAlertFilters)),
      switchMap(([, filters]) =>
        api.alerts(filters).pipe(
          map((result) => StockAlertsApiActions.loadAlertsSucceeded({ result })),
          catchError((error: ApiError) => of(StockAlertsApiActions.loadAlertsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadThresholds$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(AlertApiService)) =>
    actions$.pipe(
      ofType(
        StockAlertsPageActions.opened,
        StockAlertsPageActions.searchChanged,
        StockAlertsPageActions.thresholdsPageChanged,
        StockAlertsPageActions.refreshRequested,
      ),
      withLatestFrom(store.select(selectThresholdFilters)),
      switchMap(([, filters]) =>
        api.thresholds(filters).pipe(
          map((result) => StockAlertsApiActions.loadThresholdsSucceeded({ result })),
          catchError((error: ApiError) => of(StockAlertsApiActions.loadThresholdsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const acknowledge$ = createEffect(
  (actions$ = inject(Actions), api = inject(AlertApiService)) =>
    actions$.pipe(
      ofType(StockAlertsPageActions.acknowledgeSubmitted),
      switchMap(({ id, actingPerson }) =>
        api.acknowledge(id, actingPerson).pipe(
          map((alert) => StockAlertsApiActions.acknowledgeSucceeded({ alert })),
          catchError((error: ApiError) => of(StockAlertsApiActions.acknowledgeFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const setThreshold$ = createEffect(
  (actions$ = inject(Actions), api = inject(AlertApiService)) =>
    actions$.pipe(
      ofType(StockAlertsPageActions.thresholdSubmitted),
      switchMap(({ variantRefId, quantity, actingPerson }) =>
        api.setThreshold(variantRefId, quantity, actingPerson).pipe(
          map((threshold) => StockAlertsApiActions.thresholdSucceeded({ threshold })),
          catchError((error: ApiError) => of(StockAlertsApiActions.thresholdFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
