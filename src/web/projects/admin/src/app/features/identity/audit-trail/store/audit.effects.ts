import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { AuditApiService } from '../services/audit-api.service';
import { AuditApiActions, AuditPageActions } from './audit.actions';
import { selectAuditFilters } from './audit.selectors';

/** Re-runs the audit search whenever a filter, the page or the page size changes. */
export const loadAudit$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(AuditApiService)) =>
    actions$.pipe(
      ofType(AuditPageActions.opened, AuditPageActions.filtersApplied, AuditPageActions.pageChanged, AuditPageActions.pageSizeChanged),
      withLatestFrom(store.select(selectAuditFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => AuditApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(AuditApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
