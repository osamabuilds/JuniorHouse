import { createReducer, on } from '@ngrx/store';
import { SizeChartsApiActions, SizeChartsPageActions } from './size-charts.actions';
import { initialSizeChartsState } from './size-charts.state';

export const sizeChartsReducer = createReducer(
  initialSizeChartsState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(SizeChartsPageActions.opened, () => ({ ...initialSizeChartsState, loading: true })),
  on(SizeChartsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(SizeChartsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(SizeChartsPageActions.createStarted, (state) => ({ ...state, mode: 'create' as const, chart: null, formError: null, notice: null })),
  on(SizeChartsPageActions.editOpened, (state) => ({ ...state, mode: 'edit' as const, chart: null, formError: null, notice: null })),
  on(SizeChartsPageActions.editorClosed, (state) => ({ ...state, mode: 'list' as const, chart: null, saving: false, formError: null })),
  on(SizeChartsPageActions.createSubmitted, SizeChartsPageActions.updateSubmitted, (state) => ({ ...state, saving: true, formError: null })),
  on(SizeChartsPageActions.deleteConfirmed, (state) => ({ ...state, saving: true, formError: null })),
  on(SizeChartsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(SizeChartsApiActions.loadListSucceeded, (state, { result }) => ({
    ...state,
    rows: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(SizeChartsApiActions.loadListFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(SizeChartsApiActions.loadChartSucceeded, (state, { chart }) => ({ ...state, chart })),
  on(SizeChartsApiActions.loadChartFailed, (state, { error }) => ({ ...state, error, mode: 'list' as const })),
  on(SizeChartsApiActions.saveSucceeded, (state, { chart }) => ({
    ...state,
    mode: 'list' as const,
    chart: null,
    saving: false,
    loading: true,
    notice: `Size chart "${chart.name}" was saved. Products of that category will show it.`,
  })),
  // A stale edit (409) or a duplicate (409) is shown in the form in the server's words.
  on(SizeChartsApiActions.saveFailed, (state, { error }) => ({ ...state, saving: false, formError: error })),
  on(SizeChartsApiActions.deleteSucceeded, (state) => ({
    ...state,
    mode: 'list' as const,
    chart: null,
    saving: false,
    loading: true,
    notice: 'The size chart was deleted. Products of that category now show "size chart coming soon".',
  })),
  on(SizeChartsApiActions.deleteFailed, (state, { error }) => ({ ...state, saving: false, formError: error })),
);
