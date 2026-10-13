import { createFeatureSelector, createSelector } from '@ngrx/store';
import { RunFilters } from '../models';
import { PRODUCTION_FEATURE_KEY, ProductionState } from './production.state';

const selectProductionState = createFeatureSelector<ProductionState>(PRODUCTION_FEATURE_KEY);

// ---- list ----
export const selectRuns = createSelector(selectProductionState, (state) => state.runs);
export const selectRunsTotal = createSelector(selectProductionState, (state) => state.total);
export const selectRunsPage = createSelector(selectProductionState, (state) => state.page);
export const selectRunsPageSize = createSelector(selectProductionState, (state) => state.pageSize);
export const selectRunsLoading = createSelector(selectProductionState, (state) => state.loading);
export const selectRunsError = createSelector(selectProductionState, (state) => state.error);
export const selectStatusFilter = createSelector(selectProductionState, (state) => state.statusFilter);
export const selectSearchText = createSelector(selectProductionState, (state) => state.searchText);
export const selectAtRiskOnly = createSelector(selectProductionState, (state) => state.atRiskOnly);

export const selectRunFilters = createSelector(
  selectStatusFilter,
  selectSearchText,
  selectAtRiskOnly,
  selectRunsPage,
  selectRunsPageSize,
  (statusId, search, atRisk, page, pageSize): RunFilters => ({ statusId, search, atRisk, page, pageSize }),
);

// ---- one run ----
export const selectRun = createSelector(selectProductionState, (state) => state.run);
export const selectRunId = createSelector(selectRun, (run) => run?.id ?? null);
export const selectRunLoading = createSelector(selectProductionState, (state) => state.runLoading);
export const selectRunError = createSelector(selectProductionState, (state) => state.runError);
export const selectPanel = createSelector(selectProductionState, (state) => state.panel);
export const selectSaving = createSelector(selectProductionState, (state) => state.saving);
export const selectPanelError = createSelector(selectProductionState, (state) => state.panelError);
export const selectNotice = createSelector(selectProductionState, (state) => state.notice);
export const selectFileUploading = createSelector(selectProductionState, (state) => state.fileUploading);
export const selectFileError = createSelector(selectProductionState, (state) => state.fileError);

/** The round waiting for a decision, if any. */
export const selectPendingRound = createSelector(selectRun, (run) => run?.ppRounds.find((round) => round.decisionCode === 'Pending') ?? null);
