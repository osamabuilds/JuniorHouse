import { createReducer, on } from '@ngrx/store';
import { ProductionApiActions, ProductionPageActions, ProductionRunActions } from './production.actions';
import { initialProductionState } from './production.state';

export const productionReducer = createReducer(
  initialProductionState,

  // ---- list ----
  // The store outlives the page, so opening the list starts from a clean slate.
  on(ProductionPageActions.listOpened, () => ({ ...initialProductionState, loading: true })),
  on(ProductionPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText, page: 1, loading: true, error: null })),
  on(ProductionPageActions.statusFilterChanged, (state, { statusId }) => ({ ...state, statusFilter: statusId, page: 1, loading: true, error: null })),
  on(ProductionPageActions.atRiskToggled, (state) => ({ ...state, atRiskOnly: !state.atRiskOnly, page: 1, loading: true, error: null })),
  on(ProductionPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(ProductionPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(ProductionApiActions.loadRunsSucceeded, (state, { result }) => ({
    ...state,
    runs: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(ProductionApiActions.loadRunsFailed, (state, { error }) => ({ ...state, error, loading: false })),

  // ---- one run ----
  on(ProductionRunActions.opened, () => ({ ...initialProductionState, runLoading: true })),
  on(ProductionApiActions.loadRunSucceeded, (state, { run }) => ({ ...state, run, runLoading: false, runError: null })),
  on(ProductionApiActions.loadRunFailed, (state, { error }) => ({ ...state, runError: error, runLoading: false })),
  on(ProductionRunActions.panelOpened, (state, { panel }) => ({ ...state, panel, panelError: null, notice: null })),
  on(ProductionRunActions.panelClosed, (state) => ({ ...state, panel: 'none' as const, panelError: null })),
  on(
    ProductionRunActions.milestoneSubmitted,
    ProductionRunActions.sampleSubmitted,
    ProductionRunActions.sampleDecided,
    ProductionRunActions.expectedCompletionSubmitted,
    ProductionRunActions.deliveryNoteSubmitted,
    (state) => ({ ...state, saving: true, panelError: null, notice: null }),
  ),
  on(ProductionApiActions.actionSucceeded, (state, { notice }) => ({ ...state, saving: false, panel: 'none' as const, panelError: null, notice })),
  on(ProductionApiActions.actionFailed, (state, { error }) => ({ ...state, saving: false, panelError: error })),
  on(ProductionRunActions.sampleFileUploadRequested, (state) => ({ ...state, fileUploading: true, fileError: null })),
  on(ProductionRunActions.sampleFileRemoveRequested, (state) => ({ ...state, fileError: null })),
  on(ProductionApiActions.fileUploadSucceeded, (state) => ({ ...state, fileUploading: false })),
  on(ProductionApiActions.fileUploadFailed, (state, { error }) => ({ ...state, fileUploading: false, fileError: error })),
);
