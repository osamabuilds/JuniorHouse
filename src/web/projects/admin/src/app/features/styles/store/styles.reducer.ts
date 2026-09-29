import { createReducer, on } from '@ngrx/store';
import { StylesApiActions, StylesPageActions } from './styles.actions';
import { initialStylesState, StylesState } from './styles.state';

export const stylesReducer = createReducer(
  initialStylesState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(StylesPageActions.opened, () => ({ ...initialStylesState, loading: true })),
  on(StylesPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText, page: 1,
    loading: true, error: null })),
  on(StylesPageActions.categoryFilterChanged, (state, { categoryId }) => ({
    ...state,
    categoryFilter: categoryId,
    loading: true,
    error: null,
  })),
  on(StylesPageActions.activeOnlyToggled, (state) => ({ ...state, activeOnly: !state.activeOnly, page: 1, loading: true, error: null })),
  on(StylesPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(StylesPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(StylesPageActions.createStarted, (state) => ({
    ...state,
    mode: 'create' as const,
    editingId: null,
    editingStyle: null,
    error: null,
  })),
  on(StylesPageActions.editCancelled, (state) => ({ ...state, mode: 'list' as const, editingStyle: null })),
  on(StylesPageActions.formInvalid, (state, { error }) => ({ ...state, error })),
  on(StylesPageActions.createSubmitted, StylesPageActions.updateSubmitted, (state) => ({
    ...state,
    saving: true,
    error: null,
  })),

  on(StylesApiActions.loadStylesSucceeded, (state, { result }) => ({
    ...state,
    styles: result.items,
    total: result.totalCount,
    page: result.page,
    loading: false,
  })),
  on(StylesApiActions.loadStylesFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(StylesApiActions.loadStyleSucceeded, (state, { style }) => ({
    ...state,
    mode: 'edit' as const,
    editingId: style.id,
    editingStyle: style,
    error: null,
  })),
  on(StylesApiActions.loadStyleFailed, (state, { error }) => ({ ...state, error })),
  on(StylesApiActions.saveSucceeded, (state) => ({
    ...state,
    saving: false,
    mode: 'list' as const,
    editingStyle: null,
    loading: true,
    error: null,
  })),
  on(StylesApiActions.saveFailed, (state, { error }) => ({ ...state, saving: false, error })),
);
