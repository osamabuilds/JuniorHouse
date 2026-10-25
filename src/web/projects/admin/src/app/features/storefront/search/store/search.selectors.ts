import { createFeatureSelector, createSelector } from '@ngrx/store';
import { ListFilters } from '../models';
import { SEARCH_FEATURE_KEY, SearchAdminState } from './search.state';

const selectState = createFeatureSelector<SearchAdminState>(SEARCH_FEATURE_KEY);

export const selectSynonyms = createSelector(selectState, (s) => s.synonyms);
export const selectSynonymsTotal = createSelector(selectState, (s) => s.synonymsTotal);
export const selectSynonymsPage = createSelector(selectState, (s) => s.synonymsPage);
export const selectSynonymsPageSize = createSelector(selectState, (s) => s.synonymsPageSize);
export const selectSynonymsLoading = createSelector(selectState, (s) => s.synonymsLoading);
export const selectSynonymsError = createSelector(selectState, (s) => s.synonymsError);
export const selectSearchMode = createSelector(selectState, (s) => s.mode);
export const selectEditingGroup = createSelector(selectState, (s) => s.editing);
export const selectSearchSaving = createSelector(selectState, (s) => s.saving);
export const selectSearchFormError = createSelector(selectState, (s) => s.formError);
export const selectDeletingId = createSelector(selectState, (s) => s.deletingId);

export const selectMisses = createSelector(selectState, (s) => s.misses);
export const selectMissesTotal = createSelector(selectState, (s) => s.missesTotal);
export const selectMissesPage = createSelector(selectState, (s) => s.missesPage);
export const selectMissesPageSize = createSelector(selectState, (s) => s.missesPageSize);
export const selectMissesLoading = createSelector(selectState, (s) => s.missesLoading);
export const selectMissesError = createSelector(selectState, (s) => s.missesError);

export const selectSynonymFilters = createSelector(selectSynonymsPage, selectSynonymsPageSize, (page, pageSize): ListFilters => ({ page, pageSize }));
export const selectMissFilters = createSelector(selectMissesPage, selectMissesPageSize, (page, pageSize): ListFilters => ({ page, pageSize }));
