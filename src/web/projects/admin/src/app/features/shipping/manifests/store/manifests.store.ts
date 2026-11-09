import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store, createActionGroup, createFeatureSelector, createReducer, createSelector, emptyProps, on, props } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE, Page } from '@shared';
import { ManifestDetail, ManifestSummary } from '../models';
import { ManifestsApiService } from '../services/manifests-api.service';

export const MANIFESTS_FEATURE_KEY = 'manifests';

export interface ManifestsState {
  readonly rows: readonly ManifestSummary[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly detail: ManifestDetail | null;
  readonly loading: boolean;
  readonly busy: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialManifestsState: ManifestsState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  detail: null,
  loading: false,
  busy: false,
  error: null,
  notice: null,
};

export const ManifestsPageActions = createActionGroup({
  source: 'Manifests Page',
  events: {
    Opened: emptyProps(),
    'Page Changed': props<{ page: number }>(),
    'Page Size Changed': props<{ pageSize: number }>(),
    'Generate Clicked': emptyProps(),
    'Notice Dismissed': emptyProps(),
  },
});

export const ManifestDetailActions = createActionGroup({
  source: 'Manifest Detail Page',
  events: {
    Opened: props<{ id: number }>(),
    'Count Entered': props<{ id: number; parcels: number }>(),
    'Handover Toggled': props<{ id: number; shipmentId: number; handedOver: boolean }>(),
    'Resolve Submitted': props<{ id: number; note: string }>(),
  },
});

export const ManifestsApiActions = createActionGroup({
  source: 'Manifests API',
  events: {
    'List Succeeded': props<{ result: Page<ManifestSummary> }>(),
    'Detail Succeeded': props<{ detail: ManifestDetail }>(),
    'Generate Succeeded': props<{ count: number }>(),
    'Request Failed': props<{ error: ApiError }>(),
  },
});

export const manifestsReducer = createReducer(
  initialManifestsState,
  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(ManifestsPageActions.opened, (state) => ({ ...initialManifestsState, pageSize: state.pageSize, loading: true })),
  on(ManifestsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(ManifestsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(ManifestsPageActions.generateClicked, (state) => ({ ...state, busy: true, error: null, notice: null })),
  on(ManifestsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),
  on(ManifestDetailActions.opened, () => ({ ...initialManifestsState, loading: true })),
  on(ManifestDetailActions.countEntered, ManifestDetailActions.handoverToggled, ManifestDetailActions.resolveSubmitted, (state) => ({ ...state, busy: true, error: null })),
  on(ManifestsApiActions.listSucceeded, (state, { result }) => ({ ...state, rows: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(ManifestsApiActions.detailSucceeded, (state, { detail }) => ({ ...state, detail, loading: false, busy: false })),
  on(ManifestsApiActions.generateSucceeded, (state, { count }) => ({
    ...state,
    busy: false,
    notice: count === 0 ? 'No booked parcels are waiting for a manifest.' : `${count} ${count === 1 ? 'manifest' : 'manifests'} made or updated.`,
  })),
  on(ManifestsApiActions.requestFailed, (state, { error }) => ({ ...state, error, loading: false, busy: false })),
);

const selectState = createFeatureSelector<ManifestsState>(MANIFESTS_FEATURE_KEY);
export const selectRows = createSelector(selectState, (s) => s.rows);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectDetail = createSelector(selectState, (s) => s.detail);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectBusy = createSelector(selectState, (s) => s.busy);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);
const selectQuery = createSelector(selectPage, selectPageSize, (page, pageSize) => ({ page, pageSize }));

export const loadList$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ManifestsApiService)) =>
    actions$.pipe(
      ofType(ManifestsPageActions.opened, ManifestsPageActions.pageChanged, ManifestsPageActions.pageSizeChanged, ManifestsApiActions.generateSucceeded),
      withLatestFrom(store.select(selectQuery)),
      switchMap(([, q]) =>
        api.list(q.page, q.pageSize).pipe(
          map((result) => ManifestsApiActions.listSucceeded({ result })),
          catchError((error: ApiError) => of(ManifestsApiActions.requestFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Staff make the manifests by hand; running it twice adds to the open one and duplicates nothing (MNF-4). */
export const generate$ = createEffect(
  (actions$ = inject(Actions), api = inject(ManifestsApiService)) =>
    actions$.pipe(
      ofType(ManifestsPageActions.generateClicked),
      concatMap(() =>
        api.generate().pipe(
          map((made) => ManifestsApiActions.generateSucceeded({ count: made.length })),
          catchError((error: ApiError) => of(ManifestsApiActions.requestFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(ManifestsApiService)) =>
    actions$.pipe(
      ofType(ManifestDetailActions.opened),
      switchMap(({ id }) =>
        api.get(id).pipe(
          map((detail) => ManifestsApiActions.detailSucceeded({ detail })),
          catchError((error: ApiError) => of(ManifestsApiActions.requestFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** After a change the detail is read again, so the page shows what the server holds (MNF-5, MNF-7). */
export const count$ = createEffect(
  (actions$ = inject(Actions), api = inject(ManifestsApiService)) =>
    actions$.pipe(
      ofType(ManifestDetailActions.countEntered),
      concatMap(({ id, parcels }) =>
        api.count(id, parcels).pipe(
          switchMap(() => api.get(id)),
          map((detail) => ManifestsApiActions.detailSucceeded({ detail })),
          catchError((error: ApiError) => of(ManifestsApiActions.requestFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const handover$ = createEffect(
  (actions$ = inject(Actions), api = inject(ManifestsApiService)) =>
    actions$.pipe(
      ofType(ManifestDetailActions.handoverToggled),
      concatMap(({ id, shipmentId, handedOver }) =>
        api.handover(id, shipmentId, handedOver).pipe(
          map((detail) => ManifestsApiActions.detailSucceeded({ detail })),
          catchError((error: ApiError) => of(ManifestsApiActions.requestFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const resolve$ = createEffect(
  (actions$ = inject(Actions), api = inject(ManifestsApiService)) =>
    actions$.pipe(
      ofType(ManifestDetailActions.resolveSubmitted),
      concatMap(({ id, note }) =>
        api.resolve(id, note).pipe(
          switchMap(() => api.get(id)),
          map((detail) => ManifestsApiActions.detailSucceeded({ detail })),
          catchError((error: ApiError) => of(ManifestsApiActions.requestFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
