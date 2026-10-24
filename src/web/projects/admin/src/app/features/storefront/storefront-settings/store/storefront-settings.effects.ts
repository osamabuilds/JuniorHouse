import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, EMPTY, filter, map, of, switchMap, tap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { StorefrontSettingsApiService } from '../services/storefront-settings-api.service';
import { StorefrontSettingsApiActions as Api, StorefrontSettingsPageActions as Ui } from './storefront-settings.actions';
import { selectDraftVersion, selectPage, selectPageFilters, selectSnapshotsPage, selectSnapshotsPageSize } from './storefront-settings.selectors';

export const loadDraft$ = createEffect(
  (actions$ = inject(Actions), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.draftOpened, Ui.publishOpened),
      switchMap(() =>
        api.getDraft().pipe(
          map((draft) => Api.loadDraftSucceeded({ draft })),
          catchError((error: ApiError) => of(Api.loadDraftFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Saves one part of the draft, quoting the version it was loaded at; a stale version comes back as a conflict (ST-14). */
export const savePart$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.saveSubmitted),
      withLatestFrom(store.select(selectDraftVersion)),
      switchMap(([{ part }, version]) =>
        api.save(part, version ?? 0).pipe(
          map(({ version: next, patch }) => Api.saveSucceeded({ version: next, patch })),
          catchError((error: ApiError) => of(Api.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadPages$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.pagesOpened, Ui.pagesStatusFiltered, Ui.pagesPageChanged, Ui.pagesPageSizeChanged),
      withLatestFrom(store.select(selectPageFilters)),
      switchMap(([, filters]) =>
        api.listPages(filters).pipe(
          map((result) => Api.loadPagesSucceeded({ result })),
          catchError((error: ApiError) => of(Api.loadPagesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadPage$ = createEffect(
  (actions$ = inject(Actions), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.pageEditorOpened),
      filter(({ id }) => id !== null),
      switchMap(({ id }) =>
        api.getPage(id as number).pipe(
          map((page) => Api.loadPageSucceeded({ page })),
          catchError((error: ApiError) => of(Api.loadPageFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Creates the page when there is none yet, otherwise updates it with the row version it was loaded at (ST-14). */
export const savePage$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.pageSaveSubmitted),
      withLatestFrom(store.select(selectPage)),
      switchMap(([{ value }, page]) => {
        const request = page ? api.updatePage(page.id, page.rowVersion, value) : api.createPage(value);
        return request.pipe(
          map((saved) => Api.savePageSucceeded({ page: saved, created: page === null })),
          catchError((error: ApiError) => of(Api.savePageFailed({ error }))),
        );
      }),
    ),
  { functional: true },
);

export const changePageStatus$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.pageStatusSubmitted),
      withLatestFrom(store.select(selectPage)),
      filter(([, page]) => page !== null),
      switchMap(([{ status }, page]) =>
        api.changePageStatus(page!.id, page!.rowVersion, status).pipe(
          map((saved) => Api.savePageSucceeded({ page: saved, created: false })),
          catchError((error: ApiError) => of(Api.savePageFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** A page that was just created moves to its own address, so the next Save is an update. */
export const openCreatedPage$ = createEffect(
  (actions$ = inject(Actions), router = inject(Router)) =>
    actions$.pipe(
      ofType(Api.savePageSucceeded),
      filter(({ created }) => created),
      tap(({ page }) => void router.navigate(['/storefront/pages', page.id])),
      switchMap(() => EMPTY),
    ),
  { functional: true, dispatch: false },
);

export const loadSnapshots$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.publishOpened, Ui.snapshotsPageChanged, Api.publishSucceeded, Api.restoreSucceeded),
      withLatestFrom(store.select(selectSnapshotsPage), store.select(selectSnapshotsPageSize)),
      switchMap(([, page, pageSize]) =>
        api.listSnapshots(page, pageSize).pipe(
          map((result) => Api.loadSnapshotsSucceeded({ result })),
          catchError((error: ApiError) => of(Api.loadSnapshotsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const publish$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.publishSubmitted),
      withLatestFrom(store.select(selectDraftVersion)),
      switchMap(([{ note }, version]) =>
        api.publish(version ?? 0, note).pipe(
          map((result) => Api.publishSucceeded({ result })),
          catchError((error: ApiError) => of(Api.publishFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const restore$ = createEffect(
  (actions$ = inject(Actions), api = inject(StorefrontSettingsApiService)) =>
    actions$.pipe(
      ofType(Ui.restoreSubmitted),
      switchMap(({ versionNo, note }) =>
        api.restore(versionNo, note).pipe(
          map((result) => Api.restoreSucceeded({ result })),
          catchError((error: ApiError) => of(Api.restoreFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
