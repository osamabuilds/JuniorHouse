import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, mergeMap, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { ReferenceApiService } from '../services/reference-api.service';
import { lookupCacheKey } from '../utils/lookup-cache-key.util';
import { ReferenceDataApiActions, ReferenceDataPageActions, ReferenceLookupActions } from './reference-data.actions';
import { selectIncludeInactive, selectSelectedTypeKey } from './reference-data.selectors';

/** Reloads the visible list whenever what it shows (type, retired filter) or its contents change. */
export const loadItems$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ReferenceApiService)) =>
    actions$.pipe(
      ofType(
        ReferenceDataPageActions.opened,
        ReferenceDataPageActions.typeSelected,
        ReferenceDataPageActions.includeInactiveToggled,
        ReferenceDataApiActions.saveSucceeded,
        ReferenceDataApiActions.retireSucceeded,
      ),
      withLatestFrom(store.select(selectSelectedTypeKey), store.select(selectIncludeInactive)),
      switchMap(([, typeKey, includeInactive]) =>
        api.list(typeKey, includeInactive).pipe(
          map((items) => ReferenceDataApiActions.loadItemsSucceeded({ items })),
          catchError((error: ApiError) => of(ReferenceDataApiActions.loadItemsFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const createLookup$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReferenceApiService)) =>
    actions$.pipe(
      ofType(ReferenceDataPageActions.createSubmitted),
      switchMap(({ typeKey, value }) =>
        api.create(typeKey, value).pipe(
          map(() => ReferenceDataApiActions.saveSucceeded()),
          catchError((error: ApiError) => of(ReferenceDataApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const updateLookup$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReferenceApiService)) =>
    actions$.pipe(
      ofType(ReferenceDataPageActions.updateSubmitted),
      switchMap(({ typeKey, id, value }) =>
        api.update(typeKey, id, value).pipe(
          map(() => ReferenceDataApiActions.saveSucceeded()),
          catchError((error: ApiError) => of(ReferenceDataApiActions.saveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const retireLookup$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReferenceApiService)) =>
    actions$.pipe(
      ofType(ReferenceDataPageActions.retireConfirmed),
      switchMap(({ typeKey, id }) =>
        api.retire(typeKey, id).pipe(
          map(() => ReferenceDataApiActions.retireSucceeded()),
          catchError((error: ApiError) => of(ReferenceDataApiActions.retireFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Loads a lookup list into the shared cache; lists load side by side, never cancelling each other. */
export const loadLookups$ = createEffect(
  (actions$ = inject(Actions), api = inject(ReferenceApiService)) =>
    actions$.pipe(
      ofType(ReferenceLookupActions.requested),
      mergeMap(({ typeKey, includeInactive }) => {
        const cacheKey = lookupCacheKey(typeKey, includeInactive);
        return api.list(typeKey, includeInactive).pipe(
          map((items) => ReferenceDataApiActions.loadLookupsSucceeded({ cacheKey, items })),
          catchError((error: ApiError) => of(ReferenceDataApiActions.loadLookupsFailed({ cacheKey, error }))),
        );
      }),
    ),
  { functional: true },
);
