import { createFeatureSelector, createSelector } from '@ngrx/store';
import { AuditFilters } from '../models';
import { AUDIT_FEATURE_KEY, AuditState } from './audit.state';

const selectAuditState = createFeatureSelector<AuditState>(AUDIT_FEATURE_KEY);

export const selectAuditEntries = createSelector(selectAuditState, (state) => state.entries);
export const selectAuditTotal = createSelector(selectAuditState, (state) => state.total);
export const selectAuditPage = createSelector(selectAuditState, (state) => state.page);
export const selectAuditPageSize = createSelector(selectAuditState, (state) => state.pageSize);
export const selectAuditForm = createSelector(selectAuditState, (state) => state.form);
export const selectAuditSelected = createSelector(selectAuditState, (state) => state.selected);
export const selectAuditLoading = createSelector(selectAuditState, (state) => state.loading);
export const selectAuditError = createSelector(selectAuditState, (state) => state.error);
export const selectAuditFilters = createSelector(
  selectAuditForm,
  selectAuditPage,
  selectAuditPageSize,
  (form, page, pageSize): AuditFilters => ({ ...form, page, pageSize }),
);
