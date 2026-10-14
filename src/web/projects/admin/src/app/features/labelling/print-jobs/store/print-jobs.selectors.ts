import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PrintJobFilters } from '../models';
import { PRINT_JOBS_FEATURE_KEY, PrintJobsState } from './print-jobs.state';

const selectPrintJobsState = createFeatureSelector<PrintJobsState>(PRINT_JOBS_FEATURE_KEY);

export const selectJobs = createSelector(selectPrintJobsState, (state) => state.jobs);
export const selectTotal = createSelector(selectPrintJobsState, (state) => state.total);
export const selectPage = createSelector(selectPrintJobsState, (state) => state.page);
export const selectPageSize = createSelector(selectPrintJobsState, (state) => state.pageSize);
export const selectLoading = createSelector(selectPrintJobsState, (state) => state.loading);
export const selectError = createSelector(selectPrintJobsState, (state) => state.error);
export const selectStatus = createSelector(selectPrintJobsState, (state) => state.status);
export const selectVariantId = createSelector(selectPrintJobsState, (state) => state.variantId);
export const selectPrinters = createSelector(selectPrintJobsState, (state) => state.printers);
export const selectReprintVariantId = createSelector(selectPrintJobsState, (state) => state.reprintVariantId);
export const selectReprintSku = createSelector(selectPrintJobsState, (state) => state.reprintSku);
export const selectReprintSaving = createSelector(selectPrintJobsState, (state) => state.reprintSaving);
export const selectReprintError = createSelector(selectPrintJobsState, (state) => state.reprintError);
export const selectBusyJobId = createSelector(selectPrintJobsState, (state) => state.busyJobId);
export const selectNotice = createSelector(selectPrintJobsState, (state) => state.notice);

export const selectFilters = createSelector(
  selectStatus,
  selectVariantId,
  selectPage,
  selectPageSize,
  (status, variantId, page, pageSize): PrintJobFilters => ({ status, variantId, page, pageSize }),
);
