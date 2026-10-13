import { createReducer, on } from '@ngrx/store';
import { LabelWorkApiActions, LabelWorkPageActions } from './label-work.actions';
import { initialLabelWorkState } from './label-work.state';

export const labelWorkReducer = createReducer(
  initialLabelWorkState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(LabelWorkPageActions.opened, () => ({ ...initialLabelWorkState, loading: true })),
  on(LabelWorkPageActions.statusChanged, (state, { status }) => ({ ...state, status, page: 1, loading: true, error: null })),
  on(LabelWorkPageActions.priceFlagToggled, (state) => ({ ...state, priceFlagOnly: !state.priceFlagOnly, page: 1, loading: true, error: null })),
  on(LabelWorkPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(LabelWorkPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(LabelWorkPageActions.refreshRequested, (state) => ({ ...state, loading: true, error: null })),
  on(LabelWorkPageActions.queueFormOpened, (state, { item }) => ({ ...state, queueItem: item, queueError: null, notice: null })),
  on(LabelWorkPageActions.queueFormClosed, (state) => ({ ...state, queueItem: null, queueSaving: false, queueError: null })),
  on(LabelWorkPageActions.queueSubmitted, (state) => ({ ...state, queueSaving: true, queueError: null })),
  on(LabelWorkPageActions.resolveSubmitted, (state, { itemId }) => ({ ...state, resolvingId: itemId, error: null, notice: null })),
  on(LabelWorkPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(LabelWorkApiActions.loadItemsSucceeded, (state, { result }) => ({
    ...state,
    items: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(LabelWorkApiActions.loadItemsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(LabelWorkApiActions.loadPrintersSucceeded, (state, { printers }) => ({ ...state, printers })),
  on(LabelWorkApiActions.loadPrintersFailed, (state, { error }) => ({ ...state, error })),
  on(LabelWorkApiActions.queueSucceeded, (state, { job }) => ({
    ...state,
    queueItem: null,
    queueSaving: false,
    queueError: null,
    loading: true,
    notice: `Queued ${job.qty} tag${job.qty === 1 ? '' : 's'} as job ${job.jobNo} on ${job.printerCode}.`,
  })),
  on(LabelWorkApiActions.queueFailed, (state, { error }) => ({ ...state, queueSaving: false, queueError: error })),
  on(LabelWorkApiActions.resolveSucceeded, (state, { item }) => ({
    ...state,
    resolvingId: null,
    loading: true,
    notice: item.statusCode === 'Ready' ? `Ready to print: ${item.skuCode}.` : 'Still needs attention.',
  })),
  on(LabelWorkApiActions.resolveFailed, (state, { error }) => ({ ...state, resolvingId: null, error })),
);
