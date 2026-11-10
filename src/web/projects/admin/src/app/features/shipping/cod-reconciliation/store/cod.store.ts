import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store, createActionGroup, createFeatureSelector, createReducer, createSelector, emptyProps, on, props } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap, tap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE, Page } from '@shared';
import { CodAwaitingRow, CodExceptionRow, CodSummary, RecordRemittanceRequest, RemittanceDetail, RemittanceSummary } from '../models';
import { CodApiService } from '../services/cod-api.service';

export const COD_FEATURE_KEY = 'codReconciliation';

export interface PagedList<T> {
  readonly rows: readonly T[];
  readonly total: number;
  readonly page: number;
}

export interface CodState {
  readonly summary: CodSummary | null;
  readonly awaiting: PagedList<CodAwaitingRow>;
  readonly overdueOnly: boolean;
  readonly exceptions: PagedList<CodExceptionRow>;
  readonly remittances: PagedList<RemittanceSummary>;
  readonly detail: RemittanceDetail | null;
  readonly loading: boolean;
  readonly busy: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

const emptyList = { rows: [], total: 0, page: 1 };

export const initialCodState: CodState = {
  summary: null,
  awaiting: emptyList,
  overdueOnly: false,
  exceptions: emptyList,
  remittances: emptyList,
  detail: null,
  loading: false,
  busy: false,
  error: null,
  notice: null,
};

export type CodListName = 'awaiting' | 'exceptions' | 'remittances';

export const CodPageActions = createActionGroup({
  source: 'COD Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ list: CodListName; page: number }>(),
    'Overdue Filter Changed': props<{ overdueOnly: boolean }>(),
    'Line Resolved': props<{ lineId: number; note: string }>(),
    'Notice Dismissed': emptyProps(),
  },
});

export const CodRemittanceActions = createActionGroup({
  source: 'COD Remittance',
  events: {
    'Form Opened': emptyProps(),
    'Detail Opened': props<{ id: number }>(),
    'Record Submitted': props<{ request: RecordRemittanceRequest }>(),
    'Line Resolved': props<{ id: number; lineId: number; note: string }>(),
  },
});

export const CodApiActions = createActionGroup({
  source: 'COD API',
  events: {
    'Summary Succeeded': props<{ summary: CodSummary }>(),
    'Awaiting Succeeded': props<{ result: Page<CodAwaitingRow> }>(),
    'Exceptions Succeeded': props<{ result: Page<CodExceptionRow> }>(),
    'Remittances Succeeded': props<{ result: Page<RemittanceSummary> }>(),
    'Detail Succeeded': props<{ detail: RemittanceDetail }>(),
    'Record Succeeded': props<{ detail: RemittanceDetail; alreadyRecorded: boolean }>(),
    'Resolve Succeeded': emptyProps(),
    'Request Failed': props<{ error: ApiError }>(),
  },
});

function toList<T>(result: Page<T>): PagedList<T> {
  return { rows: result.items, total: result.totalCount, page: result.page };
}

export const codReducer = createReducer(
  initialCodState,
  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(CodPageActions.opened, () => ({ ...initialCodState, loading: true })),
  on(CodPageActions.pageChanged, (state, { list, page }) => ({ ...state, [list]: { ...state[list], page }, error: null })),
  on(CodPageActions.overdueFilterChanged, (state, { overdueOnly }) => ({ ...state, overdueOnly, awaiting: { ...state.awaiting, page: 1 } })),
  on(CodPageActions.lineResolved, CodRemittanceActions.lineResolved, CodRemittanceActions.recordSubmitted, (state) => ({ ...state, busy: true, error: null, notice: null })),
  on(CodPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),
  on(CodRemittanceActions.formOpened, () => ({ ...initialCodState })),
  on(CodRemittanceActions.detailOpened, () => ({ ...initialCodState, loading: true })),
  on(CodApiActions.summarySucceeded, (state, { summary }) => ({ ...state, summary, loading: false })),
  on(CodApiActions.awaitingSucceeded, (state, { result }) => ({ ...state, awaiting: toList(result) })),
  on(CodApiActions.exceptionsSucceeded, (state, { result }) => ({ ...state, exceptions: toList(result) })),
  on(CodApiActions.remittancesSucceeded, (state, { result }) => ({ ...state, remittances: toList(result) })),
  on(CodApiActions.detailSucceeded, (state, { detail }) => ({ ...state, detail, loading: false, busy: false })),
  on(CodApiActions.recordSucceeded, (state, { detail, alreadyRecorded }) => ({
    ...state,
    detail,
    busy: false,
    notice: alreadyRecorded ? 'This statement was recorded before, so nothing changed.' : null,
  })),
  on(CodApiActions.resolveSucceeded, (state) => ({ ...state, busy: false, notice: 'The line is resolved.' })),
  on(CodApiActions.requestFailed, (state, { error }) => ({ ...state, error, loading: false, busy: false })),
);

const selectState = createFeatureSelector<CodState>(COD_FEATURE_KEY);
export const selectSummary = createSelector(selectState, (s) => s.summary);
export const selectAwaiting = createSelector(selectState, (s) => s.awaiting);
export const selectOverdueOnly = createSelector(selectState, (s) => s.overdueOnly);
export const selectExceptions = createSelector(selectState, (s) => s.exceptions);
export const selectRemittances = createSelector(selectState, (s) => s.remittances);
export const selectDetail = createSelector(selectState, (s) => s.detail);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectBusy = createSelector(selectState, (s) => s.busy);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

const fail = (error: ApiError) => of(CodApiActions.requestFailed({ error }));

export const loadSummary$ = createEffect(
  (actions$ = inject(Actions), api = inject(CodApiService)) =>
    actions$.pipe(
      ofType(CodPageActions.opened, CodApiActions.resolveSucceeded),
      switchMap(() =>
        api.summary().pipe(
          map((summary) => CodApiActions.summarySucceeded({ summary })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const loadAwaiting$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CodApiService)) =>
    actions$.pipe(
      ofType(CodPageActions.opened, CodPageActions.overdueFilterChanged, CodPageActions.pageChanged),
      withLatestFrom(store.select(selectState)),
      switchMap(([, state]) =>
        api.awaiting(state.awaiting.page, DEFAULT_PAGE_SIZE, state.overdueOnly).pipe(
          map((result) => CodApiActions.awaitingSucceeded({ result })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const loadExceptions$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CodApiService)) =>
    actions$.pipe(
      ofType(CodPageActions.opened, CodPageActions.pageChanged, CodApiActions.resolveSucceeded),
      withLatestFrom(store.select(selectState)),
      switchMap(([, state]) =>
        api.exceptions(state.exceptions.page, DEFAULT_PAGE_SIZE).pipe(
          map((result) => CodApiActions.exceptionsSucceeded({ result })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const loadRemittances$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CodApiService)) =>
    actions$.pipe(
      ofType(CodPageActions.opened, CodPageActions.pageChanged, CodApiActions.resolveSucceeded),
      withLatestFrom(store.select(selectState)),
      switchMap(([, state]) =>
        api.remittances(state.remittances.page, DEFAULT_PAGE_SIZE).pipe(
          map((result) => CodApiActions.remittancesSucceeded({ result })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(CodApiService)) =>
    actions$.pipe(
      ofType(CodRemittanceActions.detailOpened),
      switchMap(({ id }) =>
        api.remittance(id).pipe(
          map((detail) => CodApiActions.detailSucceeded({ detail })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

/** Recording goes to the new statement; a repeat goes to the first one and says so (COD-9). */
export const record$ = createEffect(
  (actions$ = inject(Actions), api = inject(CodApiService), router = inject(Router)) =>
    actions$.pipe(
      ofType(CodRemittanceActions.recordSubmitted),
      concatMap(({ request }) =>
        api.record(request).pipe(
          tap((result) => void router.navigate(['/dispatch/cod/remittances', result.remittance.summary.id])),
          map((result) => CodApiActions.recordSucceeded({ detail: result.remittance, alreadyRecorded: result.alreadyRecorded })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

/** From the list page: resolve, then the lists are read again (COD-12). */
export const resolveFromList$ = createEffect(
  (actions$ = inject(Actions), api = inject(CodApiService)) =>
    actions$.pipe(
      ofType(CodPageActions.lineResolved),
      concatMap(({ lineId, note }) =>
        api.resolve(lineId, note).pipe(
          map(() => CodApiActions.resolveSucceeded()),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);

/** From the statement page: resolve, then the statement is read again so it shows who and why (COD-12). */
export const resolveFromDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(CodApiService)) =>
    actions$.pipe(
      ofType(CodRemittanceActions.lineResolved),
      concatMap(({ id, lineId, note }) =>
        api.resolve(lineId, note).pipe(
          switchMap(() => api.remittance(id)),
          map((detail) => CodApiActions.detailSucceeded({ detail })),
          catchError(fail),
        ),
      ),
    ),
  { functional: true },
);
