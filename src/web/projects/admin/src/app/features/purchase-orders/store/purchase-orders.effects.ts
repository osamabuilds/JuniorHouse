import { inject } from '@angular/core';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { Store } from '@ngrx/store';
import { EMPTY, Observable, catchError, filter, map, merge, mergeMap, of, switchMap, withLatestFrom } from 'rxjs';
import { ApiError } from '@core/http';
import { fetchAllPages } from '@shared';
import { CatalogApiService } from '@features/styles';
import { VendorApiService } from '@features/vendors';
import { PoDto } from '../models';
import { PO_STATUS_DRAFT } from '../purchase-orders.constants';
import { PoApiService } from '../services/po-api.service';
import { hasCommercialTerms, toUpdatePoValue } from '../utils/po-update-value.util';
import { PurchaseOrdersApiActions, PurchaseOrdersPageActions } from './purchase-orders.actions';
import { selectCurrentPo, selectPoFilters, selectSelectedStyle } from './purchase-orders.selectors';

/** Re-runs the PO search whenever a filter changes or the list is shown again. */
export const loadOrders$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(
        PurchaseOrdersPageActions.opened,
        PurchaseOrdersPageActions.vendorFilterChanged,
        PurchaseOrdersPageActions.statusFilterChanged,
        PurchaseOrdersPageActions.pageChanged,
        PurchaseOrdersPageActions.pageSizeChanged,
        PurchaseOrdersPageActions.backToListClicked,
      ),
      withLatestFrom(store.select(selectPoFilters)),
      switchMap(([, filters]) =>
        api.search(filters).pipe(
          map((result) => PurchaseOrdersApiActions.loadOrdersSucceeded({ result })),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.loadOrdersFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

export const loadVendorOptions$ = createEffect(
  (actions$ = inject(Actions), api = inject(VendorApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.opened),
      switchMap(() =>
        fetchAllPages((page, pageSize) => api.search({ searchText: '', specialisationId: null, activeOnly: true, page, pageSize })).pipe(
          map((vendors) => PurchaseOrdersApiActions.loadVendorsSucceeded({ vendors })),
          catchError(() => EMPTY),
        ),
      ),
    ),
  { functional: true },
);

export const loadStyleOptions$ = createEffect(
  (actions$ = inject(Actions), api = inject(CatalogApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.opened),
      switchMap(() =>
        fetchAllPages((page, pageSize) => api.search({ searchText: '', categoryId: null, activeOnly: true, page, pageSize })).pipe(
          map((styles) => PurchaseOrdersApiActions.loadStylesSucceeded({ styles })),
          catchError(() => EMPTY),
        ),
      ),
    ),
  { functional: true },
);

/** The picked vendor's defaults (payment term) fill in the form. */
export const loadSelectedVendor$ = createEffect(
  (actions$ = inject(Actions), api = inject(VendorApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.vendorSelected),
      switchMap(({ vendorId }) =>
        api.getById(vendorId).pipe(
          map((vendor) => PurchaseOrdersApiActions.loadVendorSucceeded({ vendor })),
          catchError(() => EMPTY),
        ),
      ),
    ),
  { functional: true },
);

/** The style's size run and colourways: on picking a style, and when editing a Draft PO. */
export const loadSelectedStyle$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CatalogApiService)) =>
    merge(
      actions$.pipe(
        ofType(PurchaseOrdersPageActions.styleSelected),
        map(({ styleId }) => styleId),
        filter((styleId): styleId is number => styleId !== null),
      ),
      actions$.pipe(
        ofType(PurchaseOrdersPageActions.draftEditStarted),
        withLatestFrom(store.select(selectCurrentPo)),
        map(([, po]) => po?.styleId ?? null),
        filter((styleId): styleId is number => styleId !== null),
      ),
    ).pipe(
      switchMap((styleId) =>
        api.getById(styleId).pipe(
          map((style) => PurchaseOrdersApiActions.loadStyleSucceeded({ style })),
          catchError(() => EMPTY),
        ),
      ),
    ),
  { functional: true },
);

export const loadDetail$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.detailRequested),
      switchMap(({ id }) =>
        api.getById(id).pipe(
          map((po) => PurchaseOrdersApiActions.loadDetailSucceeded({ po })),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.loadDetailFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Re-reads the PO after any change made from the detail view. */
export const refreshDetail$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(
        PurchaseOrdersApiActions.amendmentSucceeded,
        PurchaseOrdersApiActions.vendorResponseSucceeded,
        PurchaseOrdersApiActions.revisionDecisionSucceeded,
        PurchaseOrdersApiActions.fileUploadSucceeded,
        PurchaseOrdersApiActions.fileRemovalSucceeded,
      ),
      withLatestFrom(store.select(selectCurrentPo)),
      filter(([, po]) => po !== null),
      switchMap(([, po]) =>
        api.getById(po!.id).pipe(
          map((updated) => PurchaseOrdersApiActions.refreshDetailSucceeded({ po: updated })),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.refreshDetailFailed({ error }))),
        ),
      ),
    ),
  { functional: true },
);

/** Loads what sits beside a PO whenever the detail view gets a new copy of it: its files and revisions. */
export const loadDetailExtras$ = createEffect(
  (actions$ = inject(Actions), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(
        PurchaseOrdersApiActions.loadDetailSucceeded,
        PurchaseOrdersApiActions.saveSucceeded,
        PurchaseOrdersApiActions.sendSucceeded,
        PurchaseOrdersApiActions.cancelSucceeded,
        PurchaseOrdersApiActions.refreshDetailSucceeded,
      ),
      map(({ po }) => po),
      mergeMap((po: PoDto) => {
        const files$ = api.listFiles(po.id).pipe(
          map((files) => PurchaseOrdersApiActions.loadFilesSucceeded({ files })),
          catchError(() => EMPTY),
        );
        if (po.statusId === PO_STATUS_DRAFT) {
          return files$;
        }
        const revisions$ = api.getRevisions(po.id).pipe(
          map((revisions) => PurchaseOrdersApiActions.loadRevisionsSucceeded({ revisions })),
          catchError(() => EMPTY),
        );
        return merge(files$, revisions$);
      }),
    ),
  { functional: true },
);

/** Opening a PO also loads its style's size run, unless that style is already loaded. */
export const loadStyleForDetail$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(CatalogApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersApiActions.loadDetailSucceeded, PurchaseOrdersApiActions.saveSucceeded),
      withLatestFrom(store.select(selectSelectedStyle)),
      filter(([{ po }, style]) => style?.id !== po.styleId),
      switchMap(([{ po }]) =>
        api.getById(po.styleId).pipe(
          map((style) => PurchaseOrdersApiActions.loadStyleSucceeded({ style })),
          catchError(() => EMPTY),
        ),
      ),
    ),
  { functional: true },
);

/**
 * Creating a PO with commercial terms is a create followed by an update: the create call takes
 * only the core fields.
 */
export const savePo$ = createEffect(
  (actions$ = inject(Actions), store = inject(Store), api = inject(PoApiService)) =>
    actions$.pipe(
      ofType(PurchaseOrdersPageActions.saveSubmitted),
      withLatestFrom(store.select(selectCurrentPo)),
      switchMap(([{ poId, draft }, current]) => {
        const saved$: Observable<PoDto> =
          poId !== null
            ? api.update(poId, toUpdatePoValue(draft, current!))
            : api
                .create({
                  vendorId: draft.vendorId,
                  styleId: draft.styleId,
                  unitCost: draft.unitCost,
                  expectedDeliveryDate: draft.expectedDeliveryDate,
                  lines: draft.lines,
                  paymentTermId: draft.paymentTermId,
                  advancePercent: draft.advancePercent,
                })
                .pipe(
                  switchMap((created) =>
                    hasCommercialTerms(draft) ? api.update(created.id, toUpdatePoValue(draft, created)) : of(created),
                  ),
                );

        return saved$.pipe(
          map((po) => PurchaseOrdersApiActions.saveSucceeded({ po })),
          catchError((error: ApiError) => of(PurchaseOrdersApiActions.saveFailed({ error }))),
        );
      }),
    ),
  { functional: true },
);
