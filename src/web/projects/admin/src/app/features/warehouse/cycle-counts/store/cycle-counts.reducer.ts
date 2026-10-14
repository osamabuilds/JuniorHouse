import { createReducer, on } from '@ngrx/store';
import { COUNT_STATUS_TEXT } from '../cycle-counts.constants';
import { CycleCountsApiActions, CycleCountsPageActions } from './cycle-counts.actions';
import { initialCycleCountsState } from './cycle-counts.state';

export const cycleCountsReducer = createReducer(
  initialCycleCountsState,

  // The store outlives the page, so opening a page starts from a clean slate.
  on(CycleCountsPageActions.listOpened, () => ({ ...initialCycleCountsState, countsLoading: true, binsLoading: true })),
  // The notice survives the move from the list to the new count, so "Count CC-... started" is still on screen.
  on(CycleCountsPageActions.detailOpened, (state) => ({ ...initialCycleCountsState, detailLoading: true, notice: state.notice })),
  on(CycleCountsPageActions.statusFilterChanged, (state, { status }) => ({ ...state, statusFilter: status, countsPage: 1, countsLoading: true, error: null })),
  on(CycleCountsPageActions.countsPageChanged, (state, { page }) => ({ ...state, countsPage: page, countsLoading: true, error: null })),
  on(CycleCountsPageActions.binsPageChanged, (state, { page }) => ({ ...state, binsPage: page, binsLoading: true })),
  on(CycleCountsPageActions.binZoneChanged, (state, { zone }) => ({ ...state, binZone: zone, binsPage: 1, binsLoading: true })),
  on(CycleCountsPageActions.binToggled, (state, { binId }) => ({
    ...state,
    selectedBinIds: state.selectedBinIds.includes(binId) ? state.selectedBinIds.filter((id) => id !== binId) : [...state.selectedBinIds, binId],
  })),
  on(CycleCountsPageActions.createSubmitted, (state) => ({ ...state, creating: true, error: null })),
  on(CycleCountsPageActions.linesSubmitted, (state) => ({ ...state, submitting: true, error: null, notice: null })),
  on(CycleCountsPageActions.reviewRequested, (state) => ({ ...state, reviewLoading: true, review: null, error: null })),
  on(CycleCountsPageActions.reviewClosed, (state) => ({ ...state, review: null, choices: {} })),
  on(CycleCountsPageActions.reasonChosen, (state, { key, reasonId }) => ({
    ...state,
    choices: { ...state.choices, [key]: { reasonId, reconfirmed: state.choices[key]?.reconfirmed ?? false } },
  })),
  on(CycleCountsPageActions.reconfirmToggled, (state, { key, reconfirmed }) => ({
    ...state,
    choices: { ...state.choices, [key]: { reasonId: state.choices[key]?.reasonId ?? 0, reconfirmed } },
  })),
  on(CycleCountsPageActions.confirmSubmitted, (state) => ({ ...state, confirming: true, error: null })),
  on(CycleCountsPageActions.cancelSubmitted, (state) => ({ ...state, confirming: true, error: null })),
  on(CycleCountsPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(CycleCountsApiActions.loadCountsSucceeded, (state, { result }) => ({
    ...state,
    counts: result.items,
    countsTotal: result.totalCount,
    countsPage: result.page,
    countsLoading: false,
  })),
  on(CycleCountsApiActions.loadCountsFailed, (state, { error }) => ({ ...state, error, countsLoading: false })),
  on(CycleCountsApiActions.loadBinsSucceeded, (state, { result }) => ({
    ...state,
    bins: result.items,
    binsTotal: result.totalCount,
    binsPage: result.page,
    binsLoading: false,
  })),
  on(CycleCountsApiActions.loadBinsFailed, (state, { error }) => ({ ...state, error, binsLoading: false })),
  on(CycleCountsApiActions.loadReasonsSucceeded, (state, { reasons }) => ({ ...state, reasons })),
  on(CycleCountsApiActions.createSucceeded, (state, { count }) => ({
    ...state,
    creating: false,
    selectedBinIds: [],
    notice: `Count ${count.countNo} started for ${count.bins.length} bin(s).`,
  })),
  // A bin already in an open count is refused by the server (CC-3), in its own words.
  on(CycleCountsApiActions.createFailed, (state, { error }) => ({ ...state, creating: false, error })),
  on(CycleCountsApiActions.loadDetailSucceeded, (state, { count }) => ({ ...state, detail: count, detailLoading: false })),
  on(CycleCountsApiActions.loadDetailFailed, (state, { error }) => ({ ...state, error, detailLoading: false })),
  on(CycleCountsApiActions.submitLinesSucceeded, (state, { count }) => ({
    ...state,
    detail: count,
    submitting: false,
    review: null,
    choices: {},
    notice: 'Counts saved.',
  })),
  on(CycleCountsApiActions.submitLinesFailed, (state, { error }) => ({ ...state, submitting: false, error })),
  on(CycleCountsApiActions.loadReviewSucceeded, (state, { review }) => ({ ...state, review, reviewLoading: false })),
  on(CycleCountsApiActions.loadReviewFailed, (state, { error }) => ({ ...state, error, reviewLoading: false })),
  on(CycleCountsApiActions.confirmSucceeded, (state, { count }) => ({
    ...state,
    detail: count,
    review: null,
    choices: {},
    confirming: false,
    notice: `Count ${count.countNo} is ${COUNT_STATUS_TEXT[count.statusCode]?.toLowerCase() ?? count.statusCode.toLowerCase()}. Each difference was recorded as an adjustment.`,
  })),
  // A refusal (stock moved, a reason missing, a large change not confirmed) stays on the review with the server's words.
  on(CycleCountsApiActions.confirmFailed, (state, { error }) => ({ ...state, confirming: false, error })),
  on(CycleCountsApiActions.cancelSucceeded, (state, { count }) => ({
    ...state,
    detail: count,
    review: null,
    confirming: false,
    notice: `Count ${count.countNo} was cancelled. No stock changed.`,
  })),
  on(CycleCountsApiActions.cancelFailed, (state, { error }) => ({ ...state, confirming: false, error })),
);
