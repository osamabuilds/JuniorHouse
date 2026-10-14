import { createFeatureSelector, createSelector } from '@ngrx/store';
import { BatchFilters, DeliveryFilters } from '../models';
import { QUALITY_FEATURE_KEY, QualityState } from './quality.state';

const selectQualityState = createFeatureSelector<QualityState>(QUALITY_FEATURE_KEY);

// ---- receiving queue ----
export const selectDeliveries = createSelector(selectQualityState, (state) => state.deliveries);
export const selectDeliveriesTotal = createSelector(selectQualityState, (state) => state.deliveriesTotal);
export const selectDeliveriesPage = createSelector(selectQualityState, (state) => state.deliveriesPage);
export const selectDeliveriesPageSize = createSelector(selectQualityState, (state) => state.deliveriesPageSize);
export const selectDeliveriesLoading = createSelector(selectQualityState, (state) => state.deliveriesLoading);
export const selectDeliveriesError = createSelector(selectQualityState, (state) => state.deliveriesError);
export const selectDeliveryStatusFilter = createSelector(selectQualityState, (state) => state.deliveryStatusFilter);
export const selectDeliverySearch = createSelector(selectQualityState, (state) => state.deliverySearch);

export const selectDeliveryFilters = createSelector(
  selectDeliveryStatusFilter,
  selectDeliverySearch,
  selectDeliveriesPage,
  selectDeliveriesPageSize,
  (status, search, page, pageSize): DeliveryFilters => ({ status, search, page, pageSize }),
);

// ---- receiving one delivery ----
export const selectDelivery = createSelector(selectQualityState, (state) => state.delivery);
export const selectDeliveryLoading = createSelector(selectQualityState, (state) => state.deliveryLoading);
export const selectDeliveryError = createSelector(selectQualityState, (state) => state.deliveryError);
export const selectReceiptSaving = createSelector(selectQualityState, (state) => state.receiptSaving);
export const selectReceiptError = createSelector(selectQualityState, (state) => state.receiptError);
export const selectReceipt = createSelector(selectQualityState, (state) => state.receipt);

// ---- batch list ----
export const selectBatches = createSelector(selectQualityState, (state) => state.batches);
export const selectBatchesTotal = createSelector(selectQualityState, (state) => state.batchesTotal);
export const selectBatchesPage = createSelector(selectQualityState, (state) => state.batchesPage);
export const selectBatchesPageSize = createSelector(selectQualityState, (state) => state.batchesPageSize);
export const selectBatchesLoading = createSelector(selectQualityState, (state) => state.batchesLoading);
export const selectBatchesError = createSelector(selectQualityState, (state) => state.batchesError);
export const selectBatchStatusFilter = createSelector(selectQualityState, (state) => state.batchStatusFilter);
export const selectBatchSearch = createSelector(selectQualityState, (state) => state.batchSearch);

export const selectBatchFilters = createSelector(
  selectBatchStatusFilter,
  selectBatchSearch,
  selectBatchesPage,
  selectBatchesPageSize,
  (statusId, search, page, pageSize): BatchFilters => ({ statusId, search, page, pageSize }),
);

// ---- one batch ----
export const selectBatch = createSelector(selectQualityState, (state) => state.batch);
export const selectBatchId = createSelector(selectBatch, (batch) => batch?.id ?? null);
export const selectBatchLoading = createSelector(selectQualityState, (state) => state.batchLoading);
export const selectBatchError = createSelector(selectQualityState, (state) => state.batchError);
export const selectPanel = createSelector(selectQualityState, (state) => state.panel);
export const selectPanelTargetId = createSelector(selectQualityState, (state) => state.panelTargetId);
export const selectPlan = createSelector(selectQualityState, (state) => state.plan);
export const selectSaving = createSelector(selectQualityState, (state) => state.saving);
export const selectPanelError = createSelector(selectQualityState, (state) => state.panelError);
export const selectNotice = createSelector(selectQualityState, (state) => state.notice);
