import { createFeatureSelector, createSelector } from '@ngrx/store';
import { CountReasonInput, CountReviewLineDto, reviewKey } from '../models';
import { CYCLE_COUNTS_FEATURE_KEY, CycleCountsState } from './cycle-counts.state';

const selectState = createFeatureSelector<CycleCountsState>(CYCLE_COUNTS_FEATURE_KEY);

export const selectCounts = createSelector(selectState, (state) => state.counts);
export const selectCountsTotal = createSelector(selectState, (state) => state.countsTotal);
export const selectCountsPage = createSelector(selectState, (state) => state.countsPage);
export const selectCountsLoading = createSelector(selectState, (state) => state.countsLoading);
export const selectStatusFilter = createSelector(selectState, (state) => state.statusFilter);
export const selectBins = createSelector(selectState, (state) => state.bins);
export const selectBinsTotal = createSelector(selectState, (state) => state.binsTotal);
export const selectBinsPage = createSelector(selectState, (state) => state.binsPage);
export const selectBinsLoading = createSelector(selectState, (state) => state.binsLoading);
export const selectBinZone = createSelector(selectState, (state) => state.binZone);
export const selectSelectedBinIds = createSelector(selectState, (state) => state.selectedBinIds);
export const selectCreating = createSelector(selectState, (state) => state.creating);
export const selectDetail = createSelector(selectState, (state) => state.detail);
export const selectDetailLoading = createSelector(selectState, (state) => state.detailLoading);
export const selectSubmitting = createSelector(selectState, (state) => state.submitting);
export const selectReview = createSelector(selectState, (state) => state.review);
export const selectReviewLoading = createSelector(selectState, (state) => state.reviewLoading);
export const selectChoices = createSelector(selectState, (state) => state.choices);
export const selectConfirming = createSelector(selectState, (state) => state.confirming);
export const selectReasons = createSelector(selectState, (state) => state.reasons);
export const selectPageSize = createSelector(selectState, (state) => state.pageSize);
export const selectError = createSelector(selectState, (state) => state.error);
export const selectNotice = createSelector(selectState, (state) => state.notice);

/** The review lines that differ from the system (CC-4); zero-variance lines need nothing from the person (CC-8). */
export const selectVarianceLines = createSelector(selectReview, (review): readonly CountReviewLineDto[] =>
  (review?.lines ?? []).filter((line) => line.variance !== 0),
);

/** Whether the review can be confirmed: every bin counted, nothing stale, and a reason chosen for every variance (CC-4, CC-7). */
export const selectCanConfirm = createSelector(selectReview, selectVarianceLines, selectChoices, (review, variances, choices) =>
  review !== null && review.allBinsSubmitted && review.lines.every((line) => !line.stale) && variances.every((line) => (choices[reviewKey(line)]?.reasonId ?? 0) > 0),
);

/** The reasons to send with a confirmation, one per variance line. */
export const selectReasonInputs = createSelector(selectVarianceLines, selectChoices, (variances, choices): readonly CountReasonInput[] =>
  variances.map((line) => ({
    binId: line.binId,
    variantRefId: line.variantRefId,
    gradeId: line.gradeId,
    reasonId: choices[reviewKey(line)]?.reasonId ?? 0,
    reconfirmed: choices[reviewKey(line)]?.reconfirmed ?? false,
  })),
);
