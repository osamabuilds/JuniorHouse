import { batchSummary, inboundDelivery, inboundSummary, qcBatch, receipt } from '@app/testing/quality-fixtures';
import { BatchListActions, BatchPageActions, QualityApiActions, ReceivePageActions, ReceivingQueueActions } from './quality.actions';
import { qualityReducer } from './quality.reducer';
import { selectBatchFilters, selectDeliveryFilters } from './quality.selectors';
import { QUALITY_FEATURE_KEY, initialQualityState } from './quality.state';

const root = (state: ReturnType<typeof qualityReducer>) => ({ [QUALITY_FEATURE_KEY]: state });
const deliveriesPage = { items: [inboundSummary(), inboundSummary({ id: 12 })], page: 2, pageSize: 10, totalCount: 12, totalPages: 2 };

describe('qualityReducer: the receiving queue (spec AC-42)', () => {
  it('starts from a clean slate each time the queue is opened, showing the open deliveries first', () => {
    const dirty = { ...initialQualityState, deliverySearch: 'PO-9', deliveriesPage: 4, deliveryStatusFilter: 'received' };

    const state = qualityReducer(dirty, ReceivingQueueActions.opened());

    expect(state).toEqual({ ...initialQualityState, deliveriesLoading: true });
    expect(state.deliveryStatusFilter).toBe('open');
  });

  it('keeps total, page and page size from the API', () => {
    const state = qualityReducer(initialQualityState, QualityApiActions.loadDeliveriesSucceeded({ result: deliveriesPage }));

    expect(state.deliveries).toHaveLength(2);
    expect(state.deliveriesTotal).toBe(12);
    expect(state.deliveriesPage).toBe(2);
    expect(state.deliveriesLoading).toBe(false);
  });

  it.each([
    ['search text', ReceivingQueueActions.searchChanged({ searchText: 'PO-1' })],
    ['status filter', ReceivingQueueActions.statusFilterChanged({ status: 'received' })],
    ['page size', ReceivingQueueActions.pageSizeChanged({ pageSize: 25 })],
  ])('changing the %s goes back to page 1', (_name, action) => {
    const state = qualityReducer({ ...initialQualityState, deliveriesPage: 3 }, action);

    expect(state.deliveriesPage).toBe(1);
    expect(state.deliveriesLoading).toBe(true);
  });

  it('builds the filters the search sends from the store', () => {
    const state = qualityReducer(initialQualityState, ReceivingQueueActions.statusFilterChanged({ status: 'cancelled' }));

    expect(selectDeliveryFilters(root(state))).toEqual({ status: 'cancelled', search: '', page: 1, pageSize: 10 });
  });

  it('shows a failed load as an error and stops loading', () => {
    const state = qualityReducer(
      { ...initialQualityState, deliveriesLoading: true },
      QualityApiActions.loadDeliveriesFailed({ error: { status: 500, message: 'Something went wrong.', fieldErrors: {} } }),
    );

    expect(state.deliveriesError?.message).toBe('Something went wrong.');
    expect(state.deliveriesLoading).toBe(false);
  });
});

describe('qualityReducer: receiving one delivery (spec AC-3, AC-4)', () => {
  it('loads the delivery, then keeps the receipt that was recorded', () => {
    let state = qualityReducer(initialQualityState, ReceivePageActions.opened({ id: 11 }));
    expect(state.deliveryLoading).toBe(true);

    state = qualityReducer(state, QualityApiActions.loadDeliverySucceeded({ delivery: inboundDelivery() }));
    expect(state.delivery?.lines).toHaveLength(2);

    state = qualityReducer(state, ReceivePageActions.receiptSubmitted({ value: { inboundDeliveryId: 11, receivedDate: '2026-09-25', actingPerson: 'Sara Khan', note: null, lines: [] } }));
    expect(state.receiptSaving).toBe(true);

    state = qualityReducer(state, QualityApiActions.receiptSucceeded({ receipt: receipt() }));
    expect(state.receiptSaving).toBe(false);
    expect(state.receipt?.grnNo).toBe('GRN-2026-00001');
  });

  it('keeps a refusal (for example a duplicate) for the form to show and saves nothing', () => {
    const state = qualityReducer(
      { ...initialQualityState, receiptSaving: true },
      QualityApiActions.receiptFailed({ error: { status: 409, message: 'Delivery note DN-1 already has a goods receipt.', fieldErrors: {} } }),
    );

    expect(state.receiptSaving).toBe(false);
    expect(state.receipt).toBeNull();
    expect(state.receiptError?.status).toBe(409);
  });
});

describe('qualityReducer: the batch list and one batch', () => {
  it('lists batches with their own filters, back to page 1 on a change (AC-42)', () => {
    let state = qualityReducer(initialQualityState, BatchListActions.opened());
    state = qualityReducer(state, QualityApiActions.loadBatchesSucceeded({ result: { items: [batchSummary()], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));
    state = qualityReducer({ ...state, batchesPage: 2 }, BatchListActions.statusFilterChanged({ statusId: 2 }));

    expect(state.batchesPage).toBe(1);
    expect(selectBatchFilters(root(state))).toEqual({ statusId: 2, search: '', page: 1, pageSize: 10 });
  });

  it('opening a batch clears the previous one, and an action closes its panel with a notice', () => {
    let state = qualityReducer({ ...initialQualityState, batch: qcBatch({ id: 1 }), notice: 'old' }, BatchPageActions.opened({ id: 41 }));
    expect(state.batch).toBeNull();
    expect(state.batchLoading).toBe(true);

    state = qualityReducer(state, QualityApiActions.loadBatchSucceeded({ batch: qcBatch() }));
    state = qualityReducer(state, BatchPageActions.panelOpened({ panel: 'decide', targetId: null }));
    expect(state.panel).toBe('decide');

    state = qualityReducer(state, QualityApiActions.actionSucceeded({ notice: 'Outcome recorded.' }));
    expect(state.panel).toBe('none');
    expect(state.notice).toBe('Outcome recorded.');
    expect(state.saving).toBe(false);
  });

  it('a refused action keeps the panel open and shows the reason', () => {
    let state = qualityReducer(initialQualityState, BatchPageActions.panelOpened({ panel: 'decide', targetId: null }));
    state = qualityReducer(state, BatchPageActions.decisionSubmitted({ value: { actingPerson: 'Ali Raza', parts: [] } }));
    expect(state.saving).toBe(true);

    state = qualityReducer(state, QualityApiActions.actionFailed({ error: { status: 409, message: 'Record the child-safety check first.', fieldErrors: {} } }));

    expect(state.saving).toBe(false);
    expect(state.panel).toBe('decide');
    expect(state.panelError?.message).toContain('child-safety');
  });

  it('keeps the AQL plan that was previewed for the inspection form', () => {
    const plan = { modeId: 1, modeCode: 'Aql' as const, codeLetter: 'F', lotQty: 140, sampleQty: 20, acceptNo: 1, rejectNo: 2 };

    const state = qualityReducer(initialQualityState, QualityApiActions.planLoaded({ plan }));

    expect(state.plan?.sampleQty).toBe(20);
  });
});
