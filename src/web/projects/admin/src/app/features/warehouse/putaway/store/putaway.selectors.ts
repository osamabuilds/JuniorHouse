import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PUTAWAY_FEATURE_KEY, PutawayState } from './putaway.state';

const selectPutawayState = createFeatureSelector<PutawayState>(PUTAWAY_FEATURE_KEY);

export const selectItems = createSelector(selectPutawayState, (state) => state.items);
export const selectItemsTotal = createSelector(selectPutawayState, (state) => state.itemsTotal);
export const selectItemsPage = createSelector(selectPutawayState, (state) => state.itemsPage);
export const selectItemSearch = createSelector(selectPutawayState, (state) => state.itemSearch);
export const selectLoading = createSelector(selectPutawayState, (state) => state.loading);
export const selectError = createSelector(selectPutawayState, (state) => state.error);
export const selectScanning = createSelector(selectPutawayState, (state) => state.scanning);
export const selectScanResult = createSelector(selectPutawayState, (state) => state.scanResult);
export const selectScanMessage = createSelector(selectPutawayState, (state) => state.scanMessage);
export const selectSelection = createSelector(selectPutawayState, (state) => state.selection);
export const selectSaving = createSelector(selectPutawayState, (state) => state.saving);
export const selectConfirmError = createSelector(selectPutawayState, (state) => state.confirmError);
export const selectNotice = createSelector(selectPutawayState, (state) => state.notice);
export const selectRecords = createSelector(selectPutawayState, (state) => state.records);
export const selectRecordsTotal = createSelector(selectPutawayState, (state) => state.recordsTotal);
export const selectRecordsPage = createSelector(selectPutawayState, (state) => state.recordsPage);
export const selectRecordsLoading = createSelector(selectPutawayState, (state) => state.recordsLoading);
export const selectFocusToken = createSelector(selectPutawayState, (state) => state.focusToken);
