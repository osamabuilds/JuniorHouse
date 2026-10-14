import { createReducer, on } from '@ngrx/store';
import { StockApiActions, StockPageActions } from './stock.actions';
import { initialStockState } from './stock.state';

export const stockReducer = createReducer(
  initialStockState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(StockPageActions.opened, () => ({ ...initialStockState, loading: true })),
  on(StockPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText, page: 1, loading: true, error: null })),
  on(StockPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(StockPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(StockPageActions.refreshRequested, (state) => ({ ...state, loading: true, error: null })),
  on(StockPageActions.historyRequested, (state, { variant }) => ({
    ...state,
    historyFor: variant,
    movements: [],
    movementsTotal: 0,
    movementsPage: 1,
    movementsLoading: true,
  })),
  on(StockPageActions.historyPageChanged, (state, { page }) => ({ ...state, movementsPage: page, movementsLoading: true })),
  on(StockPageActions.historyClosed, (state) => ({ ...state, historyFor: null, movements: [], movementsTotal: 0, movementsLoading: false })),

  on(StockApiActions.loadStockSucceeded, (state, { result }) => ({
    ...state,
    rows: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(StockApiActions.loadStockFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(StockApiActions.loadMovementsSucceeded, (state, { result }) => ({
    ...state,
    movements: result.items,
    movementsTotal: result.totalCount,
    movementsPage: result.page,
    movementsLoading: false,
  })),
  on(StockApiActions.loadMovementsFailed, (state, { error }) => ({ ...state, error, movementsLoading: false })),
);
