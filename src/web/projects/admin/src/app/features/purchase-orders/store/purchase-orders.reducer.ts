import { createReducer, on } from '@ngrx/store';
import { PoDto } from '../models';
import { PO_STATUS_DRAFT } from '../purchase-orders.constants';
import { PurchaseOrdersApiActions, PurchaseOrdersPageActions, VendorPoViewActions } from './purchase-orders.actions';
import { PurchaseOrdersState, initialPurchaseOrdersState } from './purchase-orders.state';

/** A Draft PO has no revisions yet, so any previously shown revisions are cleared. */
const withCurrent = (state: PurchaseOrdersState, po: PoDto): PurchaseOrdersState => ({
  ...state,
  current: po,
  revisions: po.statusId === PO_STATUS_DRAFT ? [] : state.revisions,
});

/** Opens the detail view for a PO. */
const showDetail = (state: PurchaseOrdersState, po: PoDto): PurchaseOrdersState => ({
  ...withCurrent(state, po),
  mode: 'detail',
  panel: 'none',
  panelError: null,
});

export const purchaseOrdersReducer = createReducer(
  initialPurchaseOrdersState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(PurchaseOrdersPageActions.opened, () => ({ ...initialPurchaseOrdersState, loading: true })),
  on(PurchaseOrdersPageActions.vendorFilterChanged, (state, { vendorId }) => ({
    ...state,
    vendorFilter: vendorId,
    loading: true,
    error: null,
  })),
  on(PurchaseOrdersPageActions.statusFilterChanged, (state, { statusId }) => ({
    ...state,
    statusFilter: statusId,
    loading: true,
    error: null,
  })),
  on(PurchaseOrdersPageActions.createStarted, (state) => ({
    ...state,
    mode: 'create' as const,
    error: null,
    selectedStyle: null,
    selectedVendor: null,
  })),
  on(PurchaseOrdersPageActions.draftEditStarted, (state) => ({ ...state, mode: 'edit' as const, error: null })),
  on(PurchaseOrdersPageActions.formCancelled, (state) => ({ ...state, mode: state.current ? ('detail' as const) : ('list' as const) })),
  on(PurchaseOrdersPageActions.formInvalid, (state, { error }) => ({ ...state, error })),
  on(PurchaseOrdersPageActions.styleSelected, (state, { styleId }) => ({
    ...state,
    selectedStyle: styleId === null ? null : state.selectedStyle,
  })),
  on(PurchaseOrdersPageActions.saveSubmitted, (state) => ({ ...state, saving: true, error: null })),
  on(PurchaseOrdersPageActions.detailRequested, (state) => ({ ...state, loading: true })),
  on(PurchaseOrdersPageActions.backToListClicked, (state) => ({
    ...state,
    mode: 'list' as const,
    current: null,
    loading: true,
    error: null,
  })),
  on(PurchaseOrdersPageActions.panelOpened, (state, { panel }) => ({ ...state, panel, panelError: null, error: null })),
  on(PurchaseOrdersPageActions.panelClosed, (state) => ({ ...state, panel: 'none' as const, panelError: null })),
  on(PurchaseOrdersPageActions.sendConfirmed, PurchaseOrdersPageActions.revisionDecisionSubmitted, (state) => ({
    ...state,
    error: null,
  })),
  on(PurchaseOrdersPageActions.amendmentSubmitted, PurchaseOrdersPageActions.vendorResponseSubmitted, (state) => ({
    ...state,
    panelSaving: true,
    panelError: null,
  })),
  on(PurchaseOrdersPageActions.cancelReasonChosen, (state, { reasonId }) => ({ ...state, cancelReasonId: reasonId })),
  on(PurchaseOrdersPageActions.fileUploadSubmitted, (state) => ({ ...state, fileUploading: true, fileError: null })),
  on(PurchaseOrdersPageActions.fileRemovalSubmitted, (state) => ({ ...state, fileError: null })),

  on(PurchaseOrdersApiActions.loadOrdersSucceeded, (state, { orders }) => ({ ...state, orders, loading: false })),
  on(PurchaseOrdersApiActions.loadOrdersFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(PurchaseOrdersApiActions.loadVendorsSucceeded, (state, { vendors }) => ({ ...state, vendors })),
  on(PurchaseOrdersApiActions.loadStylesSucceeded, (state, { styles }) => ({ ...state, styles })),
  on(PurchaseOrdersApiActions.loadVendorSucceeded, (state, { vendor }) => ({ ...state, selectedVendor: vendor })),
  on(PurchaseOrdersApiActions.loadStyleSucceeded, (state, { style }) => ({ ...state, selectedStyle: style })),
  on(PurchaseOrdersApiActions.loadDetailSucceeded, (state, { po }) => ({ ...showDetail(state, po), loading: false })),
  on(PurchaseOrdersApiActions.loadDetailFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(PurchaseOrdersApiActions.refreshDetailSucceeded, (state, { po }) => withCurrent(state, po)),
  on(PurchaseOrdersApiActions.refreshDetailFailed, (state, { error }) => ({ ...state, error })),
  on(PurchaseOrdersApiActions.loadFilesSucceeded, (state, { files }) => ({ ...state, files })),
  on(PurchaseOrdersApiActions.loadRevisionsSucceeded, (state, { revisions }) => ({ ...state, revisions })),
  on(PurchaseOrdersApiActions.saveSucceeded, (state, { po }) => ({ ...showDetail(state, po), saving: false })),
  on(PurchaseOrdersApiActions.saveFailed, (state, { error }) => ({ ...state, saving: false, error })),
  on(PurchaseOrdersApiActions.sendSucceeded, (state, { po }) => ({
    ...withCurrent(state, po),
    panel: 'none' as const,
    panelError: null,
  })),
  on(PurchaseOrdersApiActions.sendFailed, (state, { error }) => ({
    ...state,
    error,
    panel: 'none' as const,
    panelError: null,
  })),
  on(PurchaseOrdersApiActions.amendmentSucceeded, (state) => ({
    ...state,
    panelSaving: false,
    panel: 'none' as const,
    panelError: null,
  })),
  on(PurchaseOrdersApiActions.amendmentFailed, PurchaseOrdersApiActions.vendorResponseFailed, (state, { error }) => ({
    ...state,
    panelSaving: false,
    panelError: error,
  })),
  on(PurchaseOrdersApiActions.vendorResponseSucceeded, (state, { suggestedCancelReasonId }) => ({
    ...state,
    panelSaving: false,
    panel: 'none' as const,
    panelError: null,
    // AC-29: a declined PO isn't cancelled automatically - Cancel opens with the reason pre-filled.
    cancelReasonId: suggestedCancelReasonId !== null ? suggestedCancelReasonId : state.cancelReasonId,
  })),
  on(PurchaseOrdersApiActions.revisionDecisionFailed, PurchaseOrdersApiActions.cancelFailed, (state, { error }) => ({
    ...state,
    error,
  })),
  on(PurchaseOrdersApiActions.cancelSucceeded, (state, { po }) => ({ ...withCurrent(state, po), cancelReasonId: null })),
  on(PurchaseOrdersApiActions.fileUploadSucceeded, (state) => ({ ...state, fileUploading: false })),
  on(PurchaseOrdersApiActions.fileUploadFailed, (state, { error }) => ({ ...state, fileUploading: false, fileError: error })),
  on(PurchaseOrdersApiActions.fileRemovalFailed, (state, { error }) => ({ ...state, fileError: error })),

  on(VendorPoViewActions.opened, (state) => ({ ...state, vendorView: null, vendorViewError: null })),
  on(PurchaseOrdersApiActions.loadVendorViewSucceeded, (state, { view }) => ({ ...state, vendorView: view })),
  on(PurchaseOrdersApiActions.loadVendorViewFailed, (state, { error }) => ({ ...state, vendorViewError: error })),
);
