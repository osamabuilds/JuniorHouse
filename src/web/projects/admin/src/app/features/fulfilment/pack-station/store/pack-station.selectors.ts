import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PACK_STATION_FEATURE_KEY, PackStationState } from './pack-station.state';

const selectState = createFeatureSelector<PackStationState>(PACK_STATION_FEATURE_KEY);

export const selectQueue = createSelector(selectState, (s) => s.queue);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectQueueLoading = createSelector(selectState, (s) => s.queueLoading);
export const selectView = createSelector(selectState, (s) => s.view);
export const selectViewLoading = createSelector(selectState, (s) => s.viewLoading);
export const selectLastScan = createSelector(selectState, (s) => s.lastScan);
export const selectWeight = createSelector(selectState, (s) => s.weight);
export const selectBusy = createSelector(selectState, (s) => s.busy);
export const selectBlockedReason = createSelector(selectState, (s) => s.blockedReason);
export const selectPacked = createSelector(selectState, (s) => s.packed);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectQueueQuery = createSelector(selectPage, selectPageSize, (page, pageSize) => ({ page, pageSize }));
