import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { NotifyDemandApiService } from '../services/notify-demand-api.service';
import { NotifyDemandApiActions, NotifyDemandPageActions } from './notify-demand.actions';
import { selectQuery } from './notify-demand.selectors';

/** Re-runs the list when the page opens, the paging changes or a refresh asks for it. */
export const loadRows$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(NotifyDemandApiService)) =>
    actions$.pipe(
      ofType(
        NotifyDemandPageActions.opened,
        NotifyDemandPageActions.pageChanged,
        NotifyDemandPageActions.pageSizeChanged,
        NotifyDemandPageActions.refreshed,
      ),
      withLatestFrom(store.select(selectQuery)),
      switchMap(([, q]) =>
        api.list(q.page, q.pageSize).pipe(
          map((result) => NotifyDemandApiActions.loadSucceeded({ result })),
          catchError((error: ApiError) => of(NotifyDemandApiActions.loadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
