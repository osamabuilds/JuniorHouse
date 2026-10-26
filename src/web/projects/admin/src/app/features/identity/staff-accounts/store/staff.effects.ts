import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { catchError, map, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { StaffApiService } from '../services/staff-api.service';
import { StaffApiActions, StaffPageActions } from './staff.actions';
import { selectStaffFilters } from './staff.selectors';

/** Re-runs the account search whenever a filter changes or an action changes the list. */
export const loadStaff$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(
        StaffPageActions.opened,
        StaffPageActions.searchChanged,
        StaffPageActions.activeOnlyToggled,
        StaffPageActions.pageChanged,
        StaffPageActions.pageSizeChanged,
        StaffPageActions.backToListClicked,
        StaffApiActions.createSucceeded,
      ),
      withLatestFrom(store.select(selectStaffFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => StaffApiActions.loadStaffSucceeded({ result })),
          catchError((error: ApiError) => of(StaffApiActions.loadStaffFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadRoles$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffPageActions.opened),
      switchMap(() =>
        api.roles().pipe(
          map((roles) => StaffApiActions.loadRolesSucceeded({ roles })),
          catchError((error: ApiError) => of(StaffApiActions.loadRolesFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffPageActions.staffSelected),
      switchMap(({ id }) => loadDetail(api, id)),
    ),
  { functional: true },
);

/** After a deactivate or reactivate the detail view shows the account as it now is. */
export const reloadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffApiActions.statusChangeSucceeded),
      switchMap(({ id }) => loadDetail(api, id)),
    ),
  { functional: true },
);

export const createStaff$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffPageActions.createSubmitted),
      switchMap(({ value }) =>
        api.create(value).pipe(
          map((password) => StaffApiActions.createSucceeded({ password })),
          catchError((error: ApiError) => of(StaffApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const deactivateStaff$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffPageActions.deactivateClicked),
      switchMap(({ id }) =>
        api.deactivate(id).pipe(
          map(() => StaffApiActions.statusChangeSucceeded({ id })),
          catchError((error: ApiError) => of(StaffApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const reactivateStaff$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffPageActions.reactivateClicked),
      switchMap(({ id }) =>
        api.reactivate(id).pipe(
          map(() => StaffApiActions.statusChangeSucceeded({ id })),
          catchError((error: ApiError) => of(StaffApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Replaces the person's roles with exactly the set sent; afterwards the detail shows the account as it now is (roles AC-6). */
export const assignRoles$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffPageActions.rolesSubmitted),
      switchMap(({ id, roleIds }) =>
        api.assignRoles(id, roleIds).pipe(
          map(() => StaffApiActions.statusChangeSucceeded({ id })),
          catchError((error: ApiError) => of(StaffApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** A reset is never retried by the client: each call makes a new password (see the plan's idempotency row). */
export const resetPassword$ = createEffect(
  (actions$ = inject(Actions), api = inject(StaffApiService)) =>
    actions$.pipe(
      ofType(StaffPageActions.resetPasswordClicked),
      switchMap(({ id }) =>
        api.resetPassword(id).pipe(
          map((password) => StaffApiActions.resetSucceeded({ password })),
          catchError((error: ApiError) => of(StaffApiActions.actionFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

function loadDetail(api: StaffApiService, id: number) {
  return api.getById(id).pipe(
    map((staff) => StaffApiActions.loadDetailSucceeded({ staff })),
    catchError((error: ApiError) => of(StaffApiActions.loadDetailFailed({ error }))),
  );
}
