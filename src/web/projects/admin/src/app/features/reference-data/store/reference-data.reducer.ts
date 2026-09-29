import { createReducer, on } from '@ngrx/store';
import { ReferenceDataApiActions, ReferenceDataPageActions } from './reference-data.actions';
import { initialReferenceDataState, ReferenceDataState } from './reference-data.state';

const startLoading = (state: ReferenceDataState): ReferenceDataState => ({ ...state, loading: true, error: null });

export const referenceDataReducer = createReducer(
  initialReferenceDataState,

  on(ReferenceDataPageActions.opened, startLoading),
  on(ReferenceDataPageActions.typeSelected, (state, { typeKey }) => ({
    ...state,
    selectedTypeKey: typeKey,
    mode: 'list' as const,
    searchText: '',
    loading: true,
    error: null,
  })),
  on(ReferenceDataPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText })),
  on(ReferenceDataPageActions.includeInactiveToggled, (state) => ({
    ...state,
    includeInactive: !state.includeInactive,
    loading: true,
    error: null,
  })),
  on(ReferenceDataPageActions.createStarted, (state) => ({ ...state, mode: 'create' as const, editingId: null })),
  on(ReferenceDataPageActions.editStarted, (state, { id }) => ({ ...state, mode: 'edit' as const, editingId: id })),
  on(ReferenceDataPageActions.editCancelled, (state) => ({ ...state, mode: 'list' as const, error: null })),
  on(ReferenceDataPageActions.formInvalid, (state, { error }) => ({ ...state, error })),
  on(ReferenceDataPageActions.createSubmitted, ReferenceDataPageActions.updateSubmitted, (state) => ({
    ...state,
    saving: true,
    error: null,
  })),

  on(ReferenceDataApiActions.loadItemsSucceeded, (state, { items }) => ({ ...state, items, loading: false })),
  on(ReferenceDataApiActions.loadItemsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(ReferenceDataApiActions.saveSucceeded, (state) => ({
    ...state,
    saving: false,
    mode: 'list' as const,
    loading: true,
    error: null,
  })),
  on(ReferenceDataApiActions.saveFailed, (state, { error }) => ({ ...state, saving: false, error })),
  on(ReferenceDataApiActions.retireSucceeded, startLoading),
  on(ReferenceDataApiActions.retireFailed, (state, { error }) => ({ ...state, error })),
  on(ReferenceDataApiActions.loadLookupsSucceeded, (state, { cacheKey, items }) => ({
    ...state,
    lookups: { ...state.lookups, [cacheKey]: items },
  })),
);
