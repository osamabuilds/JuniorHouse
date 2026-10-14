import { createReducer, on } from '@ngrx/store';
import { PutawayApiActions, PutawayPageActions } from './putaway.actions';
import { initialPutawayState } from './putaway.state';

/** What the scan says when the answer is a refusal, in words staff can act on (PUT-6, PUT-7). */
export function scanMessageFor(status: number, serverMessage: string): string {
  if (status === 404) {
    return 'That barcode is not known to the warehouse. Check the tag, or choose the item from the list.';
  }

  return serverMessage;
}

export const putawayReducer = createReducer(
  initialPutawayState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(PutawayPageActions.opened, () => ({ ...initialPutawayState, loading: true, recordsLoading: true })),
  on(PutawayPageActions.itemsPageChanged, (state, { page }) => ({ ...state, itemsPage: page, loading: true, error: null })),
  on(PutawayPageActions.itemSearchChanged, (state, { search }) => ({ ...state, itemSearch: search, itemsPage: 1, loading: true, error: null })),
  on(PutawayPageActions.recordsPageChanged, (state, { page }) => ({ ...state, recordsPage: page, recordsLoading: true })),
  on(PutawayPageActions.refreshRequested, (state) => ({ ...state, loading: true, recordsLoading: true, error: null })),
  on(PutawayPageActions.scanSubmitted, (state) => ({ ...state, scanning: true, scanResult: null, scanMessage: null, notice: null })),
  on(PutawayPageActions.scanCleared, (state) => ({ ...state, scanResult: null, scanMessage: null })),
  on(PutawayPageActions.itemSelected, (state, { selection }) => ({ ...state, selection, confirmError: null, notice: null })),
  on(PutawayPageActions.selectionCleared, (state) => ({ ...state, selection: null, confirmError: null, saving: false })),
  on(PutawayPageActions.confirmSubmitted, (state) => ({ ...state, saving: true, confirmError: null })),
  on(PutawayPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(PutawayApiActions.loadItemsSucceeded, (state, { result }) => ({
    ...state,
    items: result.items,
    itemsTotal: result.totalCount,
    itemsPage: result.page,
    loading: false,
  })),
  on(PutawayApiActions.loadItemsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(PutawayApiActions.loadRecordsSucceeded, (state, { result }) => ({
    ...state,
    records: result.items,
    recordsTotal: result.totalCount,
    recordsPage: result.page,
    recordsLoading: false,
  })),
  on(PutawayApiActions.loadRecordsFailed, (state, { error }) => ({ ...state, error, recordsLoading: false })),
  on(PutawayApiActions.scanSucceeded, (state, { result }) => ({
    ...state,
    scanning: false,
    scanResult: result,
    scanMessage: result.gtinRetired
      ? 'This barcode has been retired, so its stock cannot be put away. Ask a supervisor.'
      : result.pendingItems.length === 0
        ? 'Nothing is waiting to be put away for this barcode.'
        : null,
  })),
  on(PutawayApiActions.scanFailed, (state, { error }) => ({ ...state, scanning: false, scanResult: null, scanMessage: scanMessageFor(error.status, error.message) })),
  on(PutawayApiActions.confirmSucceeded, (state, { record }) => ({
    ...state,
    saving: false,
    selection: null,
    scanResult: null,
    scanMessage: null,
    confirmError: null,
    loading: true,
    recordsLoading: true,
    focusToken: state.focusToken + 1,
    notice: `${record.quantity} unit(s) of ${record.skuCode ?? 'the item'} put away in bin ${record.binCode}. ${record.itemPending} still to do for this item.`,
  })),
  // A refusal (wrong barcode, inactive bin, too many units) stays on the form in the server's words, so nothing typed is lost.
  on(PutawayApiActions.confirmFailed, (state, { error }) => ({ ...state, saving: false, confirmError: error })),
);
