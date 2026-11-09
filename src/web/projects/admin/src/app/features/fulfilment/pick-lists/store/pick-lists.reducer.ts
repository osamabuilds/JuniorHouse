import { createReducer, on } from '@ngrx/store';
import { PickListsApiActions, PickQueuePageActions, PickStationPageActions } from './pick-lists.actions';
import { initialPickListsState } from './pick-lists.state';

export const pickListsReducer = createReducer(
  initialPickListsState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(PickQueuePageActions.opened, (state) => ({ ...initialPickListsState, pageSize: state.pageSize, queueLoading: true })),
  on(PickQueuePageActions.pageChanged, (state, { page }) => ({ ...state, page, queueLoading: true, error: null })),
  on(PickQueuePageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, queueLoading: true, error: null })),
  on(PickQueuePageActions.refreshed, (state) => ({ ...state, queueLoading: true, error: null })),

  on(PickStationPageActions.opened, (state) => ({ ...initialPickListsState, pageSize: state.pageSize, listLoading: true })),
  on(PickStationPageActions.scanned, (state) => ({ ...state, busy: true, error: null })),
  on(PickStationPageActions.completeClicked, (state) => ({ ...state, busy: true, error: null, notice: null })),
  on(PickStationPageActions.releaseClicked, (state) => ({ ...state, busy: true, error: null })),
  on(PickStationPageActions.exceptionFlagged, (state) => ({ ...state, busy: true, raising: true, error: null, notice: null })),
  on(PickStationPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(PickListsApiActions.queueSucceeded, (state, { result }) => ({ ...state, queue: result.items, total: result.totalCount, page: result.page, queueLoading: false })),
  on(PickListsApiActions.queueFailed, (state, { error }) => ({ ...state, error, queueLoading: false })),
  on(PickListsApiActions.claimSucceeded, (state, { list }) => ({ ...state, list, listLoading: false, heldBy: null })),
  // Someone else holds the order (PCK-9) or it cannot be picked: the screen shows the list but takes no scans.
  on(PickListsApiActions.claimRefused, (state, { error }) => ({ ...state, error, heldBy: error.message, listLoading: true })),
  on(PickListsApiActions.listSucceeded, (state, { list }) => ({ ...state, list, listLoading: false, busy: false, raising: false })),
  on(PickListsApiActions.listFailed, (state, { error }) => ({ ...state, error, listLoading: false, busy: false, raising: false })),
  on(PickListsApiActions.scanSucceeded, (state, { outcome }) => ({ ...state, lastScan: outcome, busy: false })),
  on(PickListsApiActions.scanFailed, (state, { error }) => ({ ...state, error, busy: false })),
  on(PickListsApiActions.completeSucceeded, (state) => ({ ...state, busy: false, notice: 'The pick is complete. Take the order to the pack station.' })),
  on(PickListsApiActions.completeFailed, (state, { error }) => ({ ...state, error, busy: false })),
  on(PickListsApiActions.releaseSucceeded, (state) => ({ ...state, busy: false, list: null, notice: 'You let go of this pick. It is back in the queue.' })),
  on(PickListsApiActions.releaseFailed, (state, { error }) => ({ ...state, error, busy: false })),
  on(PickListsApiActions.exceptionSucceeded, (state) => ({ ...state, notice: 'The line is flagged. It waits for restock and the customer is told.' })),
  on(PickListsApiActions.exceptionFailed, (state, { error }) => ({ ...state, error, busy: false, raising: false })),
);
