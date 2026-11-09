import { createFeatureSelector, createSelector } from '@ngrx/store';
import { StaffFilters } from '../models';
import { STAFF_FEATURE_KEY, StaffState } from './staff.state';

const selectStaffState = createFeatureSelector<StaffState>(STAFF_FEATURE_KEY);

export const selectStaff = createSelector(selectStaffState, (state) => state.staff);
export const selectStaffTotal = createSelector(selectStaffState, (state) => state.total);
export const selectStaffPage = createSelector(selectStaffState, (state) => state.page);
export const selectStaffPageSize = createSelector(selectStaffState, (state) => state.pageSize);
export const selectStaffLoading = createSelector(selectStaffState, (state) => state.loading);
export const selectStaffError = createSelector(selectStaffState, (state) => state.error);
export const selectStaffMode = createSelector(selectStaffState, (state) => state.mode);
export const selectSelectedStaff = createSelector(selectStaffState, (state) => state.selected);
export const selectStaffRoles = createSelector(selectStaffState, (state) => state.roles);
export const selectStaffSaving = createSelector(selectStaffState, (state) => state.saving);
export const selectStaffSearchText = createSelector(selectStaffState, (state) => state.searchText);
export const selectStaffActiveOnly = createSelector(selectStaffState, (state) => state.activeOnly);
export const selectOneTimePassword = createSelector(selectStaffState, (state) => state.oneTimePassword);

export const selectStaffFilters = createSelector(
  selectStaffSearchText,
  selectStaffActiveOnly,
  selectStaffPage,
  selectStaffPageSize,
  (searchText, activeOnly, page, pageSize): StaffFilters => ({ searchText, activeOnly, page, pageSize }),
);
