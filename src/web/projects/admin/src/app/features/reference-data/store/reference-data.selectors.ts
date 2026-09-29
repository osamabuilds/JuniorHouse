import { createFeatureSelector, createSelector } from '@ngrx/store';
import { LOOKUP_TYPES } from '../reference-data.constants';
import { LookupDto } from '../models';
import { lookupCacheKey } from '../utils/lookup-cache-key.util';
import { REFERENCE_DATA_FEATURE_KEY, ReferenceDataState } from './reference-data.state';

const selectReferenceDataState = createFeatureSelector<ReferenceDataState>(REFERENCE_DATA_FEATURE_KEY);

export const selectReferenceDataItems = createSelector(selectReferenceDataState, (state) => state.items);
export const selectReferenceDataLoading = createSelector(selectReferenceDataState, (state) => state.loading);
export const selectReferenceDataError = createSelector(selectReferenceDataState, (state) => state.error);
export const selectReferenceDataMode = createSelector(selectReferenceDataState, (state) => state.mode);
export const selectReferenceDataSaving = createSelector(selectReferenceDataState, (state) => state.saving);
export const selectIncludeInactive = createSelector(selectReferenceDataState, (state) => state.includeInactive);
export const selectSearchText = createSelector(selectReferenceDataState, (state) => state.searchText);
export const selectSelectedTypeKey = createSelector(selectReferenceDataState, (state) => state.selectedTypeKey);
export const selectEditingId = createSelector(selectReferenceDataState, (state) => state.editingId);

export const selectSelectedType = createSelector(
  selectSelectedTypeKey,
  (key) => LOOKUP_TYPES.find((type) => type.key === key) ?? LOOKUP_TYPES[0],
);

export const selectVisibleItems = createSelector(selectReferenceDataItems, selectSearchText, (items, searchText) => {
  const search = searchText.trim().toLowerCase();
  if (!search) {
    return items;
  }
  return items.filter((item) => item.code.toLowerCase().includes(search) || item.name.toLowerCase().includes(search));
});

export const selectLookupCache = createSelector(selectReferenceDataState, (state) => state.lookups);

const NO_LOOKUPS: readonly LookupDto[] = [];

/** One cached lookup list (empty until its request completes). */
export const selectLookup = (typeKey: string, includeInactive = false) =>
  createSelector(selectLookupCache, (cache) => cache[lookupCacheKey(typeKey, includeInactive)] ?? NO_LOOKUPS);
