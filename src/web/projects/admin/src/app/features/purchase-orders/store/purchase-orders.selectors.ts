import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PoFilters, StyleCell } from '../models';
import { REVISION_STATUS_IN_FORCE, REVISION_STATUS_PENDING, TECH_PACK_SPEC } from '../purchase-orders.constants';
import { PURCHASE_ORDERS_FEATURE_KEY, PurchaseOrdersState } from './purchase-orders.state';

const selectPurchaseOrdersState = createFeatureSelector<PurchaseOrdersState>(PURCHASE_ORDERS_FEATURE_KEY);

export const selectOrders = createSelector(selectPurchaseOrdersState, (state) => state.orders);
export const selectOrdersLoading = createSelector(selectPurchaseOrdersState, (state) => state.loading);
export const selectOrdersError = createSelector(selectPurchaseOrdersState, (state) => state.error);
export const selectOrdersMode = createSelector(selectPurchaseOrdersState, (state) => state.mode);
export const selectOrdersSaving = createSelector(selectPurchaseOrdersState, (state) => state.saving);
export const selectVendorFilter = createSelector(selectPurchaseOrdersState, (state) => state.vendorFilter);
export const selectStatusFilter = createSelector(selectPurchaseOrdersState, (state) => state.statusFilter);
export const selectVendorOptions = createSelector(selectPurchaseOrdersState, (state) => state.vendors);
export const selectStyleOptions = createSelector(selectPurchaseOrdersState, (state) => state.styles);
export const selectCurrentPo = createSelector(selectPurchaseOrdersState, (state) => state.current);
export const selectSelectedStyle = createSelector(selectPurchaseOrdersState, (state) => state.selectedStyle);
export const selectSelectedVendor = createSelector(selectPurchaseOrdersState, (state) => state.selectedVendor);
export const selectRevisions = createSelector(selectPurchaseOrdersState, (state) => state.revisions);
export const selectFiles = createSelector(selectPurchaseOrdersState, (state) => state.files);
export const selectPanel = createSelector(selectPurchaseOrdersState, (state) => state.panel);
export const selectPanelSaving = createSelector(selectPurchaseOrdersState, (state) => state.panelSaving);
export const selectPanelError = createSelector(selectPurchaseOrdersState, (state) => state.panelError);
export const selectCancelReasonId = createSelector(selectPurchaseOrdersState, (state) => state.cancelReasonId);
export const selectFileUploading = createSelector(selectPurchaseOrdersState, (state) => state.fileUploading);
export const selectFileError = createSelector(selectPurchaseOrdersState, (state) => state.fileError);
export const selectVendorView = createSelector(selectPurchaseOrdersState, (state) => state.vendorView);
export const selectVendorViewError = createSelector(selectPurchaseOrdersState, (state) => state.vendorViewError);

export const selectPoFilters = createSelector(
  selectVendorFilter,
  selectStatusFilter,
  (vendorId, statusId): PoFilters => ({ vendorId, statusId }),
);

export const selectPendingRevision = createSelector(
  selectRevisions,
  (revisions) => revisions.find((revision) => revision.statusId === REVISION_STATUS_PENDING) ?? null,
);

export const selectCurrentRevisionNumber = createSelector(
  selectRevisions,
  (revisions) => revisions.find((revision) => revision.statusId === REVISION_STATUS_IN_FORCE)?.revisionNumber ?? 0,
);

export const selectHasTechPack = createSelector(selectFiles, (files) => files.some((file) => file.categoryId === TECH_PACK_SPEC));

/** The size x colour grid of the selected style. */
export const selectStyleCells = createSelector(selectSelectedStyle, (style): StyleCell[] =>
  style ? style.sizeIds.flatMap((sizeId) => style.colourIds.map((colourId) => ({ sizeId, colourId }))) : [],
);

export const selectVendorViewTotal = createSelector(
  selectVendorView,
  (view) => view?.lines.reduce((sum, line) => sum + line.qty, 0) ?? 0,
);
