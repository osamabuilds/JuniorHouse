import { binAge, countDto, countRow, pageOf, review, reviewLine } from '@app/testing/warehouse-fixtures';
import { ApiError } from '@core/http';
import { reviewKey } from '../models';
import { CycleCountsApiActions, CycleCountsPageActions } from './cycle-counts.actions';
import { cycleCountsReducer } from './cycle-counts.reducer';
import { selectCanConfirm, selectReasonInputs, selectVarianceLines } from './cycle-counts.selectors';
import { CYCLE_COUNTS_FEATURE_KEY, initialCycleCountsState } from './cycle-counts.state';

const busyBin: ApiError = { status: 409, message: 'Bin A-03-2 is already part of an open count.', fieldErrors: {} };
const stale: ApiError = { status: 409, message: 'Stock moved in bin(s) A-03-2 after the count opened. Recount them before confirming.', fieldErrors: {} };

const root = (state: typeof initialCycleCountsState) => ({ [CYCLE_COUNTS_FEATURE_KEY]: state });

describe('cycleCountsReducer (spec CC-1, CC-3, CC-4, CC-5, CC-7, CC-9, CC-11)', () => {
  it('opening the list starts from a clean slate and loads counts and bins', () => {
    const state = cycleCountsReducer({ ...initialCycleCountsState, selectedBinIds: [1], notice: 'old' }, CycleCountsPageActions.listOpened());

    expect(state).toMatchObject({ selectedBinIds: [], notice: null, countsLoading: true, binsLoading: true });
  });

  it('opening a count keeps the "count started" notice but nothing else (CC-1)', () => {
    const state = cycleCountsReducer({ ...initialCycleCountsState, notice: 'Count CC-2026-0001 started for 2 bin(s).', selectedBinIds: [1] }, CycleCountsPageActions.detailOpened({ id: 60 }));

    expect(state).toMatchObject({ notice: 'Count CC-2026-0001 started for 2 bin(s).', selectedBinIds: [], detailLoading: true });
  });

  it('ticking a bin selects it and ticking it again unselects it (CC-1)', () => {
    const once = cycleCountsReducer(initialCycleCountsState, CycleCountsPageActions.binToggled({ binId: 5 }));
    const twice = cycleCountsReducer(once, CycleCountsPageActions.binToggled({ binId: 6 }));
    expect(twice.selectedBinIds).toEqual([5, 6]);

    expect(cycleCountsReducer(twice, CycleCountsPageActions.binToggled({ binId: 5 })).selectedBinIds).toEqual([6]);
  });

  it('narrowing the bins to one zone goes back to their first page and reloads them (CC-11)', () => {
    const state = cycleCountsReducer({ ...initialCycleCountsState, binsPage: 5, selectedBinIds: [3] }, CycleCountsPageActions.binZoneChanged({ zone: 'A' }));

    expect(state).toMatchObject({ binZone: 'A', binsPage: 1, binsLoading: true, selectedBinIds: [3] });
  });

  it('keeps the counts and the bins to choose from (CC-11)', () => {
    const counts = cycleCountsReducer(initialCycleCountsState, CycleCountsApiActions.loadCountsSucceeded({ result: pageOf([countRow()], { totalCount: 9 }) }));
    const both = cycleCountsReducer(counts, CycleCountsApiActions.loadBinsSucceeded({ result: pageOf([binAge()], { totalCount: 40 }) }));

    expect(both).toMatchObject({ countsTotal: 9, binsTotal: 40, countsLoading: false, binsLoading: false });
  });

  it('starting a count clears the ticks and announces it; a bin already in an open count is refused in the server\'s words (CC-3)', () => {
    const started = cycleCountsReducer({ ...initialCycleCountsState, selectedBinIds: [5], creating: true }, CycleCountsApiActions.createSucceeded({ count: countDto() }));
    expect(started).toMatchObject({ selectedBinIds: [], creating: false });
    expect(started.notice).toBe('Count CC-2026-0001 started for 2 bin(s).');

    const refused = cycleCountsReducer({ ...initialCycleCountsState, selectedBinIds: [5], creating: true }, CycleCountsApiActions.createFailed({ error: busyBin }));
    expect(refused).toMatchObject({ creating: false, error: busyBin, selectedBinIds: [5] });
  });

  it('saving counts keeps the blind read, closes any review and forgets old choices (CC-2)', () => {
    const start = { ...initialCycleCountsState, submitting: true, review: review(), choices: { '5:31:1': { reasonId: 2, reconfirmed: false } } };

    const state = cycleCountsReducer(start, CycleCountsApiActions.submitLinesSucceeded({ count: countDto() }));

    expect(state).toMatchObject({ submitting: false, review: null, choices: {}, notice: 'Counts saved.' });
    expect(state.detail?.lines[0]).not.toHaveProperty('systemQuantity');
  });

  it('choosing a reason and ticking the large-change confirmation are kept per line (CC-4)', () => {
    const key = reviewKey(reviewLine());
    const chosen = cycleCountsReducer(initialCycleCountsState, CycleCountsPageActions.reasonChosen({ key, reasonId: 2 }));
    const confirmed = cycleCountsReducer(chosen, CycleCountsPageActions.reconfirmToggled({ key, reconfirmed: true }));

    expect(confirmed.choices[key]).toEqual({ reasonId: 2, reconfirmed: true });
  });

  it('a line is only confirmable once it has a reason; zero-variance lines need none (CC-4, CC-8)', () => {
    const lines = [reviewLine(), reviewLine({ variantRefId: 32, variance: 0, countedQuantity: 4, systemQuantity: 4 })];
    const base = { ...initialCycleCountsState, review: review(lines) };

    expect(selectCanConfirm(root(base))).toBe(false);
    expect(selectVarianceLines(root(base))).toHaveLength(1);

    const withReason = { ...base, choices: { [reviewKey(lines[0])]: { reasonId: 2, reconfirmed: false } } };
    expect(selectCanConfirm(root(withReason))).toBe(true);
    expect(selectReasonInputs(root(withReason))).toEqual([{ binId: 5, variantRefId: 31, gradeId: 1, reasonId: 2, reconfirmed: false }]);
  });

  it('a stale line, or a bin not yet counted, blocks confirmation even with every reason chosen (CC-7, CC-4)', () => {
    const line = reviewLine({ stale: true });
    const choices = { [reviewKey(line)]: { reasonId: 2, reconfirmed: false } };

    expect(selectCanConfirm(root({ ...initialCycleCountsState, review: review([line]), choices }))).toBe(false);
    expect(selectCanConfirm(root({ ...initialCycleCountsState, review: review([reviewLine()], { allBinsSubmitted: false }), choices }))).toBe(false);
    expect(selectCanConfirm(root(initialCycleCountsState))).toBe(false);
  });

  it('confirming closes the count and says the differences were recorded; a refusal stays on the review (CC-5, CC-7)', () => {
    const closed = cycleCountsReducer({ ...initialCycleCountsState, review: review(), confirming: true }, CycleCountsApiActions.confirmSucceeded({ count: countDto({ statusCode: 'CLOSED' }) }));
    expect(closed).toMatchObject({ review: null, confirming: false });
    expect(closed.notice).toContain('is closed');
    expect(closed.notice).toContain('recorded as an adjustment');

    const refused = cycleCountsReducer({ ...initialCycleCountsState, review: review(), confirming: true }, CycleCountsApiActions.confirmFailed({ error: stale }));
    expect(refused.review).not.toBeNull();
    expect(refused).toMatchObject({ confirming: false, error: stale });
  });

  it('cancelling changes no stock and says so (CC-9)', () => {
    const state = cycleCountsReducer({ ...initialCycleCountsState, confirming: true }, CycleCountsApiActions.cancelSucceeded({ count: countDto({ statusCode: 'CANCELLED' }) }));

    expect(state.notice).toBe('Count CC-2026-0001 was cancelled. No stock changed.');
    expect(state.confirming).toBe(false);
  });
});
