import { createFeatureSelector, createSelector } from '@ngrx/store';
import { VendorFilters } from '../models';
import { VENDORS_FEATURE_KEY, VendorsState } from './vendors.state';

const selectVendorsState = createFeatureSelector<VendorsState>(VENDORS_FEATURE_KEY);

export const selectVendors = createSelector(selectVendorsState, (state) => state.vendors);
export const selectVendorsTotal = createSelector(selectVendorsState, (state) => state.total);
export const selectVendorsPage = createSelector(selectVendorsState, (state) => state.page);
export const selectVendorsPageSize = createSelector(selectVendorsState, (state) => state.pageSize);
export const selectVendorsLoading = createSelector(selectVendorsState, (state) => state.loading);
export const selectVendorsError = createSelector(selectVendorsState, (state) => state.error);
export const selectVendorsMode = createSelector(selectVendorsState, (state) => state.mode);
export const selectVendorsSaving = createSelector(selectVendorsState, (state) => state.saving);
export const selectEditingVendor = createSelector(selectVendorsState, (state) => state.editingVendor);
export const selectSearchText = createSelector(selectVendorsState, (state) => state.searchText);
export const selectSpecialisationFilter = createSelector(selectVendorsState, (state) => state.specialisationFilter);
export const selectActiveOnly = createSelector(selectVendorsState, (state) => state.activeOnly);

export const selectVendorFilters = createSelector(
  selectSearchText,
  selectSpecialisationFilter,
  selectActiveOnly,
  selectVendorsPage,
  selectVendorsPageSize,
  (searchText, specialisationId, activeOnly, page, pageSize): VendorFilters => ({ searchText, specialisationId, activeOnly, page, pageSize }),
);
