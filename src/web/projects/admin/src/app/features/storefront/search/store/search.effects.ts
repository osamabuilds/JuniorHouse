import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { SearchAdminApiService } from '../services/search-admin-api.service';
import { SearchAdminApiActions as Api, SearchAdminPageActions as Ui } from './search.actions';
import { selectMissFilters, selectSynonymFilters } from './search.selectors';

/** Reloads the synonym groups when the screen opens, the page changes, or a save or delete changes the list. */
export const loadSynonyms$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(SearchAdminApiService)) =>
    actions$.pipe(
      ofType(Ui.synonymsOpened, Ui.synonymsPageChanged, Ui.synonymsPageSizeChanged, Api.saveSucceeded, Api.deleteSucceeded),
      withLatestFrom(store.select(selectSynonymFilters)),
      switchMap(([, filters]) =>
        api.listSynonyms(filters).pipe(
          map((result) => Api.loadSynonymsSucceeded({ result })),
          catchError((error: ApiError) => of(Api.loadSynonymsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const createSynonym$ = createEffect(
  (actions$ = inject(Actions), api = inject(SearchAdminApiService)) =>
    actions$.pipe(
      ofType(Ui.createSubmitted),
      switchMap(({ value }) =>
        api.createSynonym(value).pipe(
          map(() => Api.saveSucceeded()),
          catchError((error: ApiError) => of(Api.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const updateSynonym$ = createEffect(
  (actions$ = inject(Actions), api = inject(SearchAdminApiService)) =>
    actions$.pipe(
      ofType(Ui.updateSubmitted),
      switchMap(({ id, rowVersion, value }) =>
        api.updateSynonym(id, rowVersion, value).pipe(
          map(() => Api.saveSucceeded()),
          catchError((error: ApiError) => of(Api.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const deleteSynonym$ = createEffect(
  (actions$ = inject(Actions), api = inject(SearchAdminApiService)) =>
    actions$.pipe(
      ofType(Ui.deleteConfirmed),
      switchMap(({ id }) =>
        api.deleteSynonym(id).pipe(
          map(() => Api.deleteSucceeded()),
          catchError((error: ApiError) => of(Api.deleteFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadMisses$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(SearchAdminApiService)) =>
    actions$.pipe(
      ofType(Ui.missesOpened, Ui.missesPageChanged, Ui.missesPageSizeChanged),
      withLatestFrom(store.select(selectMissFilters)),
      switchMap(([, filters]) =>
        api.listMisses(filters).pipe(
          map((result) => Api.loadMissesSucceeded({ result })),
          catchError((error: ApiError) => of(Api.loadMissesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
