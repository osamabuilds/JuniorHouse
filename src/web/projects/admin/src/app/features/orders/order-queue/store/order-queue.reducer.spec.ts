import { NO_FILTERS, OrderDetail, OrderListItem } from '../models';
import { OrderQueueActions, OrderQueueApiActions } from './order-queue.actions';
import { orderQueueReducer } from './order-queue.reducer';
import { initialOrderQueueState } from './order-queue.state';

const item: OrderListItem = {
  id: 7, orderNumber: 'RMP-2026-00007-1', statusId: 1, statusName: 'Pending confirmation', methodName: 'Cash on Delivery', total: 2860, placedAt: '2026-11-03T10:00:00Z',
  contactName: 'Ayesha Khan', cityName: 'Lahore', itemCount: 3, codNextDueAt: null, codAutoCancelAt: null, isFirstOrder: true, isHighValue: false, isRefusalFlagged: false,
};
const detail = { id: 7, orderNumber: 'RMP-2026-00007-1', statusName: 'Confirmed' } as OrderDetail;
const apiError = { status: 409, message: 'This order is no longer waiting for a confirmation call.' } as never;

describe('orderQueueReducer', () => {
  it('OQ-1 opening starts from a clean slate and asks for the list', () => {
    const state = orderQueueReducer({ ...initialOrderQueueState, notice: 'old', detail }, OrderQueueActions.listOpened());

    expect(state.loading).toBe(true);
    expect(state.notice).toBeNull();
    expect(state.detail).toBeNull();
  });

  it('OQ-1 a loaded page fills the table and the total', () => {
    const state = orderQueueReducer(initialOrderQueueState, OrderQueueApiActions.loadSucceeded({ result: { items: [item], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));

    expect(state.orders).toEqual([item]);
    expect(state.total).toBe(1);
    expect(state.loading).toBe(false);
  });

  it('OQ-9 changing a filter goes back to the first page and loads again', () => {
    const state = orderQueueReducer({ ...initialOrderQueueState, page: 3 }, OrderQueueActions.filtersChanged({ filters: { ...NO_FILTERS, callDueNow: true } }));

    expect(state.page).toBe(1);
    expect(state.loading).toBe(true);
    expect(state.filters.callDueNow).toBe(true);
  });

  it('OQ-2 an action waits, then shows the updated order and a notice', () => {
    const waiting = orderQueueReducer(initialOrderQueueState, OrderQueueActions.outcomeRecorded({ id: 7, outcome: 'confirmed', note: '' }));
    const done = orderQueueReducer(waiting, OrderQueueApiActions.actionSucceeded({ order: detail, notice: 'The order is confirmed.' }));

    expect(waiting.acting).toBe(true);
    expect(done.acting).toBe(false);
    expect(done.detail).toBe(detail);
    expect(done.notice).toBe('The order is confirmed.');
  });

  it('OQ-8 a refused action shows the server words and keeps the open order', () => {
    const open = orderQueueReducer(initialOrderQueueState, OrderQueueApiActions.detailSucceeded({ order: detail }));
    const failed = orderQueueReducer(orderQueueReducer(open, OrderQueueActions.orderCancelled({ id: 7, note: '' })), OrderQueueApiActions.actionFailed({ error: apiError }));

    expect(failed.acting).toBe(false);
    expect(failed.error).toBe(apiError);
    expect(failed.detail).toBe(detail);
  });
});
