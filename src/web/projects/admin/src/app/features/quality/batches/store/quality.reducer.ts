import { createReducer, on } from '@ngrx/store';
import { BatchListActions, BatchPageActions, QualityApiActions, ReceivePageActions, ReceivingQueueActions } from './quality.actions';
import { initialQualityState } from './quality.state';

export const qualityReducer = createReducer(
  initialQualityState,

  // ---- receiving queue ----
  // The store outlives the page, so opening a list starts from a clean slate.
  on(ReceivingQueueActions.opened, () => ({ ...initialQualityState, deliveriesLoading: true })),
  on(ReceivingQueueActions.searchChanged, (state, { searchText }) => ({ ...state, deliverySearch: searchText, deliveriesPage: 1, deliveriesLoading: true, deliveriesError: null })),
  on(ReceivingQueueActions.statusFilterChanged, (state, { status }) => ({ ...state, deliveryStatusFilter: status, deliveriesPage: 1, deliveriesLoading: true, deliveriesError: null })),
  on(ReceivingQueueActions.pageChanged, (state, { page }) => ({ ...state, deliveriesPage: page, deliveriesLoading: true, deliveriesError: null })),
  on(ReceivingQueueActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, deliveriesPageSize: pageSize, deliveriesPage: 1, deliveriesLoading: true, deliveriesError: null })),
  on(QualityApiActions.loadDeliveriesSucceeded, (state, { result }) => ({
    ...state,
    deliveries: result.items,
    deliveriesTotal: result.totalCount,
    deliveriesPage: result.page,
    deliveriesLoading: false,
  })),
  on(QualityApiActions.loadDeliveriesFailed, (state, { error }) => ({ ...state, deliveriesError: error, deliveriesLoading: false })),

  // ---- receiving one delivery ----
  on(ReceivePageActions.opened, () => ({ ...initialQualityState, deliveryLoading: true })),
  on(QualityApiActions.loadDeliverySucceeded, (state, { delivery }) => ({ ...state, delivery, deliveryLoading: false, deliveryError: null })),
  on(QualityApiActions.loadDeliveryFailed, (state, { error }) => ({ ...state, deliveryError: error, deliveryLoading: false })),
  on(ReceivePageActions.receiptSubmitted, (state) => ({ ...state, receiptSaving: true, receiptError: null })),
  on(QualityApiActions.receiptSucceeded, (state, { receipt }) => ({ ...state, receiptSaving: false, receipt })),
  on(QualityApiActions.receiptFailed, (state, { error }) => ({ ...state, receiptSaving: false, receiptError: error })),

  // ---- batch list ----
  on(BatchListActions.opened, () => ({ ...initialQualityState, batchesLoading: true })),
  on(BatchListActions.searchChanged, (state, { searchText }) => ({ ...state, batchSearch: searchText, batchesPage: 1, batchesLoading: true, batchesError: null })),
  on(BatchListActions.statusFilterChanged, (state, { statusId }) => ({ ...state, batchStatusFilter: statusId, batchesPage: 1, batchesLoading: true, batchesError: null })),
  on(BatchListActions.pageChanged, (state, { page }) => ({ ...state, batchesPage: page, batchesLoading: true, batchesError: null })),
  on(BatchListActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, batchesPageSize: pageSize, batchesPage: 1, batchesLoading: true, batchesError: null })),
  on(QualityApiActions.loadBatchesSucceeded, (state, { result }) => ({
    ...state,
    batches: result.items,
    batchesTotal: result.totalCount,
    batchesPage: result.page,
    batchesLoading: false,
  })),
  on(QualityApiActions.loadBatchesFailed, (state, { error }) => ({ ...state, batchesError: error, batchesLoading: false })),

  // ---- one batch ----
  on(BatchPageActions.opened, () => ({ ...initialQualityState, batchLoading: true })),
  on(QualityApiActions.loadBatchSucceeded, (state, { batch }) => ({ ...state, batch, batchLoading: false, batchError: null })),
  on(QualityApiActions.loadBatchFailed, (state, { error }) => ({ ...state, batchError: error, batchLoading: false })),
  on(BatchPageActions.panelOpened, (state, { panel, targetId }) => ({ ...state, panel, panelTargetId: targetId, panelError: null, notice: null })),
  on(BatchPageActions.panelClosed, (state) => ({ ...state, panel: 'none' as const, panelTargetId: null, panelError: null })),
  on(QualityApiActions.planLoaded, (state, { plan }) => ({ ...state, plan })),
  on(QualityApiActions.planFailed, (state) => ({ ...state, plan: null })),
  on(
    BatchPageActions.inspectionSubmitted,
    BatchPageActions.safetyChecksSubmitted,
    BatchPageActions.decisionSubmitted,
    BatchPageActions.partDecisionSubmitted,
    BatchPageActions.reworkReturnSubmitted,
    (state) => ({ ...state, saving: true, panelError: null, notice: null }),
  ),
  on(QualityApiActions.actionSucceeded, (state, { notice }) => ({
    ...state,
    saving: false,
    panel: 'none' as const,
    panelTargetId: null,
    panelError: null,
    notice,
  })),
  on(QualityApiActions.actionFailed, (state, { error }) => ({ ...state, saving: false, panelError: error })),
);
