import { createReducer, on } from '@ngrx/store';
import { VendorsApiActions, VendorsPageActions } from './vendors.actions';
import { initialVendorsState, VendorsState } from './vendors.state';

export const vendorsReducer = createReducer(
  initialVendorsState,

  // The store outlives the page, so opening it starts from a clean slate.
  on(VendorsPageActions.opened, () => ({ ...initialVendorsState, loading: true })),
  on(VendorsPageActions.searchChanged, (state, { searchText }) => ({ ...state, searchText, loading: true, error: null })),
  on(VendorsPageActions.specialisationFilterChanged, (state, { specialisationId }) => ({
    ...state,
    specialisationFilter: specialisationId,
    loading: true,
    error: null,
  })),
  on(VendorsPageActions.activeOnlyToggled, (state) => ({ ...state, activeOnly: !state.activeOnly, loading: true, error: null })),
  on(VendorsPageActions.createStarted, (state) => ({
    ...state,
    mode: 'create' as const,
    editingId: null,
    editingVendor: null,
    error: null,
  })),
  on(VendorsPageActions.editCancelled, (state) => ({ ...state, mode: 'list' as const, editingVendor: null })),
  on(VendorsPageActions.formInvalid, (state, { error }) => ({ ...state, error })),
  on(VendorsPageActions.createSubmitted, VendorsPageActions.updateSubmitted, (state) => ({
    ...state,
    saving: true,
    error: null,
  })),

  on(VendorsApiActions.loadVendorsSucceeded, (state, { vendors }) => ({ ...state, vendors, loading: false })),
  on(VendorsApiActions.loadVendorsFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(VendorsApiActions.loadVendorSucceeded, (state, { vendor }) => ({
    ...state,
    mode: 'edit' as const,
    editingId: vendor.id,
    editingVendor: vendor,
    error: null,
  })),
  on(VendorsApiActions.loadVendorFailed, (state, { error }) => ({ ...state, error })),
  on(VendorsApiActions.saveSucceeded, (state) => ({
    ...state,
    saving: false,
    mode: 'list' as const,
    editingVendor: null,
    loading: true,
    error: null,
  })),
  on(VendorsApiActions.saveFailed, (state, { error }) => ({ ...state, saving: false, error })),
);
