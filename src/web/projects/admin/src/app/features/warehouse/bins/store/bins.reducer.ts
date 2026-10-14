import { createReducer, on } from '@ngrx/store';
import { BinsApiActions, BinsPageActions } from './bins.actions';
import { initialBinsState } from './bins.state';

export const binsReducer = createReducer(
  initialBinsState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(BinsPageActions.opened, () => ({ ...initialBinsState, loading: true })),
  on(BinsPageActions.zoneChanged, (state, { zone }) => ({ ...state, zone, page: 1, loading: true, error: null })),
  on(BinsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(BinsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(BinsPageActions.refreshRequested, (state) => ({ ...state, loading: true, error: null })),
  on(BinsPageActions.formOpened, (state) => ({ ...state, formOpen: true, formError: null, notice: null })),
  on(BinsPageActions.formClosed, (state) => ({ ...state, formOpen: false, saving: false, formError: null })),
  on(BinsPageActions.createSubmitted, (state) => ({ ...state, saving: true, formError: null })),
  on(BinsPageActions.activeToggled, (state, { bin }) => ({ ...state, togglingId: bin.id, error: null, notice: null })),
  on(BinsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(BinsApiActions.loadBinsSucceeded, (state, { result }) => ({
    ...state,
    bins: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(BinsApiActions.loadBinsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(BinsApiActions.createSucceeded, (state, { bin }) => ({
    ...state,
    formOpen: false,
    saving: false,
    formError: null,
    loading: true,
    notice: `Bin ${bin.binCode} was added.`,
  })),
  on(BinsApiActions.createFailed, (state, { error }) => ({ ...state, saving: false, formError: error })),
  on(BinsApiActions.toggleSucceeded, (state, { bin }) => ({
    ...state,
    togglingId: null,
    bins: state.bins.map((b) => (b.id === bin.id ? bin : b)),
    notice: bin.isActive ? `Bin ${bin.binCode} is active again.` : `Bin ${bin.binCode} was deactivated.`,
  })),
  // A refusal (the bin still holds stock, BIN-4) is shown as the error, in the server's words.
  on(BinsApiActions.toggleFailed, (state, { error }) => ({ ...state, togglingId: null, error })),
);
