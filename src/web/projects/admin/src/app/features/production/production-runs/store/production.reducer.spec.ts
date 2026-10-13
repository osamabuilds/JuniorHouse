import { runDetail, runSummary, round } from '@app/testing/production-fixtures';
import { ProductionApiActions, ProductionPageActions, ProductionRunActions } from './production.actions';
import { productionReducer } from './production.reducer';
import { selectPendingRound, selectRunFilters, selectRunId } from './production.selectors';
import { PRODUCTION_FEATURE_KEY, initialProductionState } from './production.state';

const root = (state: ReturnType<typeof productionReducer>) => ({ [PRODUCTION_FEATURE_KEY]: state });
const page = { items: [runSummary(), runSummary({ id: 6 })], page: 2, pageSize: 10, totalCount: 12, totalPages: 2 };

describe('productionReducer (spec AC-32)', () => {
  it('starts the list from a clean slate each time it is opened', () => {
    const dirty = { ...initialProductionState, searchText: 'PO-9', atRiskOnly: true, page: 4, statusFilter: 2 };

    const state = productionReducer(dirty, ProductionPageActions.listOpened());

    expect(state).toEqual({ ...initialProductionState, loading: true });
  });

  it('keeps total, page and page size from the API', () => {
    const state = productionReducer(initialProductionState, ProductionApiActions.loadRunsSucceeded({ result: page }));

    expect(state.runs).toHaveLength(2);
    expect(state.total).toBe(12);
    expect(state.page).toBe(2);
    expect(state.loading).toBe(false);
  });

  it.each([
    ['search text', ProductionPageActions.searchChanged({ searchText: 'PO-1' })],
    ['status filter', ProductionPageActions.statusFilterChanged({ statusId: 1 })],
    ['at-risk filter', ProductionPageActions.atRiskToggled()],
    ['page size', ProductionPageActions.pageSizeChanged({ pageSize: 50 })],
  ])('changing the %s goes back to page 1 and reloads', (_name, action) => {
    const onPageThree = { ...initialProductionState, page: 3 };

    const state = productionReducer(onPageThree, action);

    expect(state.page).toBe(1);
    expect(state.loading).toBe(true);
  });

  it('changing the page keeps the filters', () => {
    const filtered = { ...initialProductionState, searchText: 'PO-1', atRiskOnly: true };

    const state = productionReducer(filtered, ProductionPageActions.pageChanged({ page: 2 }));

    expect(state.page).toBe(2);
    expect(selectRunFilters(root(state))).toEqual({ statusId: null, search: 'PO-1', atRisk: true, page: 2, pageSize: 10 });
  });

  it('records a failed list load without losing the filters', () => {
    const error = { status: 500, message: 'Server error', fieldErrors: {} };

    const state = productionReducer({ ...initialProductionState, searchText: 'x', loading: true }, ProductionApiActions.loadRunsFailed({ error }));

    expect(state.error).toEqual(error);
    expect(state.loading).toBe(false);
    expect(state.searchText).toBe('x');
  });

  it('opening a run resets the previous run so another run is never shown', () => {
    const previous = productionReducer(initialProductionState, ProductionApiActions.loadRunSucceeded({ run: runDetail({ id: 1 }) }));

    const state = productionReducer(previous, ProductionRunActions.opened({ id: 2 }));

    expect(state.run).toBeNull();
    expect(state.runLoading).toBe(true);
  });

  it('shows the loaded run and finds its id and pending round', () => {
    const run = runDetail({ ppRounds: [round({ decisionCode: 'Rejected', roundNo: 1 }), round({ id: 2, roundNo: 2 })] });

    const state = productionReducer(initialProductionState, ProductionApiActions.loadRunSucceeded({ run }));

    expect(selectRunId(root(state))).toBe(5);
    expect(selectPendingRound(root(state))?.roundNo).toBe(2);
  });

  it('opens and closes an action panel, clearing the previous error', () => {
    const failed = productionReducer(
      { ...initialProductionState, panelError: { status: 409, message: 'no', fieldErrors: {} } },
      ProductionRunActions.panelOpened({ panel: 'delivery' }),
    );

    expect(failed.panel).toBe('delivery');
    expect(failed.panelError).toBeNull();
    expect(productionReducer(failed, ProductionRunActions.panelClosed()).panel).toBe('none');
  });

  it('marks a submitted action as saving, then closes the panel with a notice when it works', () => {
    const open = { ...initialProductionState, panel: 'milestone' as const };
    const saving = productionReducer(
      open,
      ProductionRunActions.milestoneSubmitted({
        value: { typeId: 1, claimedDate: '2026-09-03', reportedBy: 'Ali', actingPerson: 'Osama', note: null, finishedLines: null },
      }),
    );
    expect(saving.saving).toBe(true);

    const done = productionReducer(saving, ProductionApiActions.actionSucceeded({ notice: 'Milestone recorded.' }));

    expect(done.saving).toBe(false);
    expect(done.panel).toBe('none');
    expect(done.notice).toBe('Milestone recorded.');
  });

  it('keeps the panel open and shows the API refusal when an action fails (AC-9)', () => {
    const open = { ...initialProductionState, panel: 'milestone' as const, saving: true };
    const error = { status: 409, message: 'Bulk cutting cannot be recorded until a pre-production sample has been approved.', fieldErrors: {} };

    const state = productionReducer(open, ProductionApiActions.actionFailed({ error }));

    expect(state.saving).toBe(false);
    expect(state.panel).toBe('milestone');
    expect(state.panelError).toEqual(error);
  });

  it('tracks a file upload separately from the panel actions', () => {
    const uploading = productionReducer(initialProductionState, ProductionRunActions.sampleFileUploadRequested({ upload: { roundNo: 1, file: new File(['x'], 'a.png') } }));
    expect(uploading.fileUploading).toBe(true);

    const failed = productionReducer(uploading, ProductionApiActions.fileUploadFailed({ error: { status: 400, message: 'not allowed', fieldErrors: {} } }));
    expect(failed.fileUploading).toBe(false);
    expect(failed.fileError?.message).toBe('not allowed');

    expect(productionReducer(failed, ProductionApiActions.fileUploadSucceeded()).fileUploading).toBe(false);
  });
});
