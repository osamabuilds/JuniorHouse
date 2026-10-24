import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PageFilters } from '../models';
import { STOREFRONT_SETTINGS_FEATURE_KEY, StorefrontSettingsState } from './storefront-settings.state';

const selectState = createFeatureSelector<StorefrontSettingsState>(STOREFRONT_SETTINGS_FEATURE_KEY);

export const selectDraft = createSelector(selectState, (state) => state.draft);
export const selectDraftVersion = createSelector(selectDraft, (draft) => draft?.version ?? null);
export const selectLoading = createSelector(selectState, (state) => state.loading);
export const selectLoadError = createSelector(selectState, (state) => state.loadError);
export const selectSaving = createSelector(selectState, (state) => state.saving);
export const selectSaveError = createSelector(selectState, (state) => state.saveError);
export const selectNotice = createSelector(selectState, (state) => state.notice);

export const selectPages = createSelector(selectState, (state) => state.pages);
export const selectPagesTotal = createSelector(selectState, (state) => state.pagesTotal);
export const selectPagesPage = createSelector(selectState, (state) => state.pagesPage);
export const selectPagesPageSize = createSelector(selectState, (state) => state.pagesPageSize);
export const selectPagesStatus = createSelector(selectState, (state) => state.pagesStatus);
export const selectPagesLoading = createSelector(selectState, (state) => state.pagesLoading);
export const selectPagesError = createSelector(selectState, (state) => state.pagesError);
export const selectPageFilters = createSelector(
  selectPagesStatus,
  selectPagesPage,
  selectPagesPageSize,
  (status, page, pageSize): PageFilters => ({ status, page, pageSize }),
);

export const selectPage = createSelector(selectState, (state) => state.page);
export const selectPageLoading = createSelector(selectState, (state) => state.pageLoading);
export const selectPageSaving = createSelector(selectState, (state) => state.pageSaving);
export const selectPageError = createSelector(selectState, (state) => state.pageError);
export const selectPageNotice = createSelector(selectState, (state) => state.pageNotice);

export const selectSnapshots = createSelector(selectState, (state) => state.snapshots);
export const selectSnapshotsTotal = createSelector(selectState, (state) => state.snapshotsTotal);
export const selectSnapshotsPage = createSelector(selectState, (state) => state.snapshotsPage);
export const selectSnapshotsPageSize = createSelector(selectState, (state) => state.snapshotsPageSize);
export const selectSnapshotsLoading = createSelector(selectState, (state) => state.snapshotsLoading);
export const selectPublishing = createSelector(selectState, (state) => state.publishing);
export const selectRestoringVersion = createSelector(selectState, (state) => state.restoringVersion);
export const selectPublishError = createSelector(selectState, (state) => state.publishError);
export const selectPublishNotice = createSelector(selectState, (state) => state.publishNotice);
