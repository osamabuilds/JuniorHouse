import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { catchError, map, of, switchMap } from 'rxjs';
import { ApiError } from '@core/http';
import { PoApiService } from '../services/po-api.service';
import { PurchaseOrdersApiActions, PurchaseOrdersPageActions } from './purchase-orders.actions';

/** AC-39: `sendWithoutTechPack` is the explicit "send anyway" from the confirmation step. */
export const sendPo$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.sendConfirmed),
      switchMap(({ poId, sendWithoutTechPack }) =>
        api.send(poId, sendWithoutTechPack).pipe(
          map((po) => PurchaseOrdersApiActions.sendSucceeded({ po })),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.sendFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const amendPo$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.amendmentSubmitted),
      switchMap(({ poId, value }) =>
        api.createAmendment(poId, value).pipe(
          map(() => PurchaseOrdersApiActions.amendmentSucceeded()),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.amendmentFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const recordVendorResponse$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.vendorResponseSubmitted),
      switchMap(({ poId, value }) =>
        api.recordVendorResponse(poId, value).pipe(
          map((result) =>
            PurchaseOrdersApiActions.vendorResponseSucceeded({ suggestedCancelReasonId: result.suggestedCancelReasonId }),
          ),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.vendorResponseFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const decideRevision$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.revisionDecisionSubmitted),
      switchMap(({ decision }) => {
        const request$ =
          decision.kind === 'accept'
            ? api.acceptRevision(decision.poId, decision.revisionNumber)
            : decision.kind === 'reject'
              ? api.rejectRevision(decision.poId, decision.revisionNumber, decision.note)
              : api.withdrawRevision(decision.poId, decision.revisionNumber, decision.note);

        return request$.pipe(
          map(() => PurchaseOrdersApiActions.revisionDecisionSucceeded()),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.revisionDecisionFailed({ error }))),
        );
      }),
    ),
  { functional: true },
);

/** An open Pending revision is auto-withdrawn by the cancel (AC-21), so revisions reload afterwards. */
export const cancelPo$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.cancelConfirmed),
      switchMap(({ poId, reasonId }) =>
        api.cancel(poId, reasonId).pipe(
          map((po) => PurchaseOrdersApiActions.cancelSucceeded({ po })),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.cancelFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const uploadPoFile$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.fileUploadSubmitted),
      switchMap(({ poId, upload }) =>
        api.uploadFile(poId, upload.categoryId, upload.file).pipe(
          map(() => PurchaseOrdersApiActions.fileUploadSucceeded()),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.fileUploadFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const removePoFile$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.fileRemovalSubmitted),
      switchMap(({ poId, fileId }) =>
        api.removeFile(poId, fileId).pipe(
          map(() => PurchaseOrdersApiActions.fileRemovalSucceeded()),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.fileRemovalFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);
