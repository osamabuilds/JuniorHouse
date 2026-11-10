import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store, createActionGroup, createFeatureSelector, createReducer, createSelector, emptyProps, on, props } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE, Page } from '@shared';
import { DecideRequest, ReceiveRequest, ReturnCaseDetail, ReturnCaseSummary } from '../models';
import { ReverseQcApiService } from '../services/reverse-qc-api.service';

export const REVERSE_QC_FEATURE_KEY = 'reverseQc';

export interface ReverseQcState {
  readonly cases: readonly ReturnCaseSummary[];
  readonly total: number;
  readonly page: number;
  readonly status: string;
  readonly detail: ReturnCaseDetail | null;
  readonly loading: boolean;
  readonly busy: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialReverseQcState: ReverseQcState = {
  cases: [],
  total: 0,
  page: 1,
  status: '',
  detail: null,
  loading: false,
  busy: false,
  error: null,
  notice: null,
};

export const CasesPageActions = createActionGroup({
  source: 'Return Cases Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Status Changed': props<{ status: string }>(),
  },
});

export const CaseDetailActions = createActionGroup({
  source: 'Return Case Page',
  events: {
    Opened: props<{ id: number }>(),
    'Receive Submitted': props<{ id: number; request: ReceiveRequest }>(),
    'Line Decided': props<{ id: number; lineId: number; request: DecideRequest }>(),
    'Complete Submitted': props<{ id: number }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const ReverseQcApiActions = createActionGroup({
  source: 'Reverse QC API',
  events: {
    'Cases Succeeded': props<{ result: Page<ReturnCaseSummary> }>(),
    'Case Succeeded': props<{ detail: ReturnCaseDetail; notice: string | null }>(),
    'Request Failed': props<{ error: ApiError }>(),
  },
});

export const reverseQcReducer = createReducer(
  initialReverseQcState,
  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(CasesPageActions.opened, () => ({ ...initialReverseQcState, loading: true })),
  on(CasesPageActions.pageChanged, (state, { page }) => ({ ...state, page, error: null })),
  on(CasesPageActions.statusChanged, (state, { status }) => ({ ...state, status, page: 1, error: null })),
  on(CaseDetailActions.opened, () => ({ ...initialReverseQcState, loading: true })),
  on(CaseDetailActions.receiveSubmitted, CaseDetailActions.lineDecided, CaseDetailActions.completeSubmitted, (state) => ({ ...state, busy: true, error: null, notice: null })),
  on(CaseDetailActions.noticeDismissed, (state) => ({ ...state, notice: null })),
  on(ReverseQcApiActions.casesSucceeded, (state, { result }) => ({ ...state, cases: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(ReverseQcApiActions.caseSucceeded, (state, { detail, notice }) => ({ ...state, detail, notice, loading: false, busy: false })),
  on(ReverseQcApiActions.requestFailed, (state, { error }) => ({ ...state, error, loading: false, busy: false })),
);

const selectState = createFeatureSelector<ReverseQcState>(REVERSE_QC_FEATURE_KEY);
export const selectCases = createSelector(selectState, (s) => s.cases);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectStatus = createSelector(selectState, (s) => s.status);
export const selectDetail = createSelector(selectState, (s) => s.detail);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectBusy = createSelector(selectState, (s) => s.busy);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

const fail = (error: ApiError) => of(ReverseQcApiActions.requestFailed({ error }));

export const loadCases$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ReverseQcApiService)) =>
    actions$.pipe(
      ofType(CasesPageActions.opened, CasesPageActions.pageChanged, CasesPageActions.statusChanged),
      withLatestFrom(store.select(selectState)),
      switchMap(([, state]) =>
        api.cases(state.page, DEFAULT_PAGE_SIZE, state.status).pipe(
          map((result) => ReverseQcApiActions.casesSucceeded({ result })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const loadCase$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReverseQcApiService)) =>
    actions$.pipe(
      ofType(CaseDetailActions.opened),
      switchMap(({ id }) =>
        api.case(id).pipe(
          map((detail) => ReverseQcApiActions.caseSucceeded({ detail, notice: null })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

/** Receiving an intact parcel restocks and completes it in one step (RQC-2); otherwise it waits for inspection (RQC-3). */
export const receive$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReverseQcApiService)) =>
    actions$.pipe(
      ofType(CaseDetailActions.receiveSubmitted),
      concatMap(({ id, request }) =>
        api.receive(id, request).pipe(
          map((detail) =>
            ReverseQcApiActions.caseSucceeded({
              detail,
              notice: detail.status === 'COMPLETED' ? 'The seal was intact, so every line is back in stock and the case is complete.' : 'Received. Now decide each line.',
            }),
          ),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const decide$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReverseQcApiService)) =>
    actions$.pipe(
      ofType(CaseDetailActions.lineDecided),
      concatMap(({ id, lineId, request }) =>
        api.decide(id, lineId, request).pipe(
          map((detail) => ReverseQcApiActions.caseSucceeded({ detail, notice: 'The line is decided.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const complete$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReverseQcApiService)) =>
    actions$.pipe(
      ofType(CaseDetailActions.completeSubmitted),
      concatMap(({ id }) =>
        api.complete(id).pipe(
          map((detail) => ReverseQcApiActions.caseSucceeded({ detail, notice: 'The case is complete.' })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);
