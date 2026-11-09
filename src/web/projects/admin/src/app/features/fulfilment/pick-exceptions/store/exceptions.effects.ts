import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, concatMap, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { ExceptionsApiService } from '../services/exceptions-api.service';
import { ExceptionsApiActions, ExceptionsPageActions } from './exceptions.actions';
import { selectQuery } from './exceptions.selectors';

/** Re-runs the list when the page opens, the filter or the paging changes, or a refresh asks for it. */
export const loadRows$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(ExceptionsApiService)) =>
    actions$.pipe(
      ofType(
        ExceptionsPageActions.opened,
        ExceptionsPageActions.filterChanged,
        ExceptionsPageActions.pageChanged,
        ExceptionsPageActions.pageSizeChanged,
        ExceptionsPageActions.refreshed,
      ),
      withLatestFrom(store.select(selectQuery)),
      switchMap(([, q]) =>
        api.list(q.openOnly, q.page, q.pageSize).pipe(
          map((result) => ExceptionsApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(ExceptionsApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const resolve$ = createEffect(
  (actions$ = inject(Actions), api = inject(ExceptionsApiService)) =>
    actions$.pipe(
      ofType(ExceptionsPageActions.resolveConfirmed),
      concatMap(({ id, action }) =>
        api.resolve(id, action).pipe(
          map((row) => ExceptionsApiActions.resolveSucceeded({ row })),
          catchError((error: ApiError) => of(ExceptionsApiActions.resolveFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const refreshAfterResolve$ = createEffect(
  (actions$ = inject(Actions)) => actions$.pipe(ofType(ExceptionsApiActions.resolveSucceeded), map(() => ExceptionsPageActions.refreshed())),
  { functional: true },
);
