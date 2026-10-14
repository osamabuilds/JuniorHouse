import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of, switchMap } from 'rxjs';
import { ApiError } from '@core/http';
import { PoApiService } from '../services/po-api.service';
import { PurchaseOrdersApiActions, VendorPoViewActions } from './purchase-orders.actions';

export const loadVendorView$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(VendorPoViewActions.opened),
      switchMap(({ poId }) =>
        api.getVendorView(poId).pipe(
          map((view) => PurchaseOrdersApiActions.loadVendorViewSucceeded({ view })),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.loadVendorViewFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
