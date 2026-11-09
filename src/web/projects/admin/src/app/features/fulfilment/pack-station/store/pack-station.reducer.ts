import { createReducer, on } from '@ngrx/store';
import { PackQueuePageActions, PackStationApiActions, PackStationPageActions } from './pack-station.actions';
import { initialPackStationState } from './pack-station.state';

export const packStationReducer = createReducer(
  initialPackStationState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(PackQueuePageActions.opened, (state) => ({ ...initialPackStationState, pageSize: state.pageSize, queueLoading: true })),
  on(PackQueuePageActions.pageChanged, (state, { page }) => ({ ...state, page, queueLoading: true, error: null })),
  on(PackQueuePageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, queueLoading: true, error: null })),
  on(PackQueuePageActions.refreshed, (state) => ({ ...state, queueLoading: true, error: null })),

  on(PackStationPageActions.opened, (state) => ({ ...initialPackStationState, pageSize: state.pageSize, viewLoading: true })),
  on(PackStationPageActions.scanned, (state) => ({ ...state, busy: true, error: null })),
  on(PackStationPageActions.weightSubmitted, (state) => ({ ...state, busy: true, error: null })),
  on(PackStationPageActions.completeClicked, (state) => ({ ...state, busy: true, error: null })),
  on(PackStationPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(PackStationApiActions.queueSucceeded, (state, { result }) => ({ ...state, queue: result.items, total: result.totalCount, page: result.page, queueLoading: false })),
  on(PackStationApiActions.queueFailed, (state, { error }) => ({ ...state, error, queueLoading: false })),
  on(PackStationApiActions.openSucceeded, (state, { view }) => ({ ...state, view, viewLoading: false, blockedReason: null })),
  on(PackStationApiActions.openRefused, (state, { error }) => ({ ...state, error: null, blockedReason: error.message, viewLoading: true })),
  on(PackStationApiActions.viewSucceeded, (state, { view }) => ({ ...state, view, viewLoading: false, busy: false, packed: view.status === 'PACKED' })),
  on(PackStationApiActions.viewFailed, (state, { error }) => ({ ...state, error, viewLoading: false, busy: false })),
  on(PackStationApiActions.scanSucceeded, (state, { outcome }) => ({ ...state, lastScan: outcome })),
  on(PackStationApiActions.scanFailed, (state, { error }) => ({ ...state, error, busy: false })),
  on(PackStationApiActions.weightSucceeded, (state, { outcome }) => ({ ...state, weight: outcome })),
  on(PackStationApiActions.weightFailed, (state, { error }) => ({ ...state, error, busy: false })),
  on(PackStationApiActions.completeSucceeded, (state) => ({ ...state, packed: true, notice: 'The parcel is sealed. Print the slip and put it inside.' })),
  on(PackStationApiActions.completeFailed, (state, { error }) => ({ ...state, error, busy: false })),
);
