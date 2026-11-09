import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PICK_LISTS_FEATURE_KEY, PickListsState } from './pick-lists.state';

const selectState = createFeatureSelector<PickListsState>(PICK_LISTS_FEATURE_KEY);

export const selectQueue = createSelector(selectState, (s) => s.queue);
export const selectTotal = createSelector(selectState, (s) => s.total);
export const selectPage = createSelector(selectState, (s) => s.page);
export const selectPageSize = createSelector(selectState, (s) => s.pageSize);
export const selectQueueLoading = createSelector(selectState, (s) => s.queueLoading);
export const selectList = createSelector(selectState, (s) => s.list);
export const selectListLoading = createSelector(selectState, (s) => s.listLoading);
export const selectLastScan = createSelector(selectState, (s) => s.lastScan);
export const selectBusy = createSelector(selectState, (s) => s.busy);
export const selectRaising = createSelector(selectState, (s) => s.raising);
export const selectHeldBy = createSelector(selectState, (s) => s.heldBy);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);

export const selectQueueQuery = createSelector(selectPage, selectPageSize, (page, pageSize) => ({ page, pageSize }));
