import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PACKING_SLIPS_FEATURE_KEY, PackingSlipsState } from './packing-slips.state';

const selectState = createFeatureSelector<PackingSlipsState>(PACKING_SLIPS_FEATURE_KEY);

export const selectSlip = createSelector(selectState, (s) => s.slip);
export const selectLoading = createSelector(selectState, (s) => s.loading);
export const selectPrinting = createSelector(selectState, (s) => s.printing);
export const selectError = createSelector(selectState, (s) => s.error);
export const selectNotice = createSelector(selectState, (s) => s.notice);
