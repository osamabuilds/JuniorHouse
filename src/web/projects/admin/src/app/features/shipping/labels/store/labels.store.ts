import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { createActionGroup, createFeatureSelector, createReducer, createSelector, on, props } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap } from 'rxjs';
import { ApiError } from '@core/http';
import { ShippingLabel } from '../models';
import { LabelsApiService } from '../services/labels-api.service';

export const LABELS_FEATURE_KEY = 'labels';

export interface LabelsState {
  readonly label: ShippingLabel | null;
  readonly loading: boolean;
  readonly error: ApiError | null;
}

export const initialLabelsState: LabelsState = { label: null, loading: false, error: null };

export const LabelPageActions = createActionGroup({
  source: 'Label Page',
  events: {
    Opened: props<{ id: number }>(),
    /** The label was sent to the printer, so the next print is a reprint (LBL-5). */
    Printed: props<{ id: number }>(),
  },
});

export const LabelsApiActions = createActionGroup({
  source: 'Labels API',
  events: {
    'Load Succeeded': props<{ label: ShippingLabel }>(),
    'Load Failed': props<{ error: ApiError }>(),
    'Printed Recorded': props<{ id: number }>(),
    'Printed Failed': props<{ error: ApiError }>(),
  },
});

export const labelsReducer = createReducer(
  initialLabelsState,
  // The store outlives the page, so opening starts from a clean slate and a label is never left on screen after leaving it (LBL-7).
  on(LabelPageActions.opened, () => ({ ...initialLabelsState, loading: true })),
  on(LabelsApiActions.loadSucceeded, (state, { label }) => ({ ...state, label, loading: false })),
  on(LabelsApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(LabelsApiActions.printedFailed, (state, { error }) => ({ ...state, error })),
);

const selectState = createFeatureSelector<LabelsState>(LABELS_FEATURE_KEY);
export const selectLabel = createSelector(selectState, (s) => s.label);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectError = createSelector(selectState, (s) => s.error);

export const loadLabel$ = createEffect(
  (actions$ = inject(Actions), api = inject(LabelsApiService)) =>
    actions$.pipe(
      ofType(LabelPageActions.opened, LabelsApiActions.printedRecorded),
      switchMap((action) =>
        api.get(action.id).pipe(
          map((label) => LabelsApiActions.loadSucceeded({ label })),
          catchError((error: ApiError) => of(LabelsApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const recordPrinted$ = createEffect(
  (actions$ = inject(Actions), api = inject(LabelsApiService)) =>
    actions$.pipe(
      ofType(LabelPageActions.printed),
      concatMap(({ id }) =>
        api.markPrinted(id).pipe(
          map(() => LabelsApiActions.printedRecorded({ id })),
          catchError((error: ApiError) => of(LabelsApiActions.printedFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
