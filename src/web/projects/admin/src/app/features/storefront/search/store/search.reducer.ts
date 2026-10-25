import { createReducer, on } from '@ngrx/store';
import { SearchAdminApiActions, SearchAdminPageActions } from './search.actions';
import { initialSearchAdminState, SearchAdminState } from './search.state';

export const searchAdminReducer = createReducer(
  initialSearchAdminState,

  // The store outlives the page, so opening a screen starts it from a clean slate.
  on(SearchAdminPageActions.synonymsOpened, (state): SearchAdminState => ({ ...initialSearchAdminState, misses: state.misses, synonymsLoading: true })),
  on(SearchAdminPageActions.synonymsPageChanged, (state, { page }): SearchAdminState => ({ ...state, synonymsPage: page, synonymsLoading: true, synonymsError: null })),
  on(SearchAdminPageActions.synonymsPageSizeChanged, (state, { pageSize }): SearchAdminState => ({ ...state, synonymsPageSize: pageSize, synonymsPage: 1, synonymsLoading: true, synonymsError: null })),

  on(SearchAdminPageActions.createStarted, (state): SearchAdminState => ({ ...state, mode: 'create', editing: null, formError: null, deletingId: null })),
  on(SearchAdminPageActions.editStarted, (state, { group }): SearchAdminState => ({ ...state, mode: 'edit', editing: group, formError: null, deletingId: null })),
  on(SearchAdminPageActions.editCancelled, (state): SearchAdminState => ({ ...state, mode: 'list', editing: null, formError: null })),
  on(SearchAdminPageActions.formInvalid, (state, { error }): SearchAdminState => ({ ...state, formError: error })),
  on(SearchAdminPageActions.createSubmitted, SearchAdminPageActions.updateSubmitted, (state): SearchAdminState => ({ ...state, saving: true, formError: null })),
  on(SearchAdminPageActions.deleteRequested, (state, { id }): SearchAdminState => ({ ...state, deletingId: id })),
  on(SearchAdminPageActions.deleteCancelled, (state): SearchAdminState => ({ ...state, deletingId: null })),
  on(SearchAdminPageActions.deleteConfirmed, (state): SearchAdminState => ({ ...state, synonymsError: null })),

  on(SearchAdminApiActions.loadSynonymsSucceeded, (state, { result }): SearchAdminState => ({
    ...state,
    synonyms: result.items,
    synonymsTotal: result.totalCount,
    synonymsPage: result.page,
    synonymsLoading: false,
  })),
  on(SearchAdminApiActions.loadSynonymsFailed, (state, { error }): SearchAdminState => ({ ...state, synonymsError: error, synonymsLoading: false })),
  on(SearchAdminApiActions.saveSucceeded, (state): SearchAdminState => ({ ...state, saving: false, mode: 'list', editing: null, formError: null, synonymsLoading: true })),
  on(SearchAdminApiActions.saveFailed, (state, { error }): SearchAdminState => ({ ...state, saving: false, formError: error })),
  on(SearchAdminApiActions.deleteSucceeded, (state): SearchAdminState => ({ ...state, deletingId: null, synonymsLoading: true })),
  on(SearchAdminApiActions.deleteFailed, (state, { error }): SearchAdminState => ({ ...state, deletingId: null, synonymsError: error })),

  on(SearchAdminPageActions.missesOpened, (state): SearchAdminState => ({ ...state, missesPage: 1, missesLoading: true, missesError: null })),
  on(SearchAdminPageActions.missesPageChanged, (state, { page }): SearchAdminState => ({ ...state, missesPage: page, missesLoading: true, missesError: null })),
  on(SearchAdminPageActions.missesPageSizeChanged, (state, { pageSize }): SearchAdminState => ({ ...state, missesPageSize: pageSize, missesPage: 1, missesLoading: true, missesError: null })),
  on(SearchAdminApiActions.loadMissesSucceeded, (state, { result }): SearchAdminState => ({
    ...state,
    misses: result.items,
    missesTotal: result.totalCount,
    missesPage: result.page,
    missesLoading: false,
  })),
  on(SearchAdminApiActions.loadMissesFailed, (state, { error }): SearchAdminState => ({ ...state, missesError: error, missesLoading: false })),
);
