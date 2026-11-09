import { DeliveryDetail, DeliveryLogRow, NO_FILTERS } from '../models';
import { DeliveryLogApiActions, DeliveryLogPageActions } from './delivery-log.actions';
import { deliveryLogReducer } from './delivery-log.reducer';
import { initialDeliveryLogState } from './delivery-log.state';

const row: DeliveryLogRow = {
  id: 11, notificationId: 4, kind: 'ORCF', channel: 'EMAL', status: 'FAIL', attempts: 1, lastError: 'PERM', orderId: 7, orderNumber: 'RMP-2026-00007-1',
  recipient: 'a***@mail.com', createdAt: '2026-11-04T09:00:00Z', nextAttemptAt: null, isResend: false, resentBy: null, canResend: true,
};
const detail = { row, warnsOfDuplicate: false, attempts: [] } as unknown as DeliveryDetail;
const apiError = { status: 409, message: 'A resend of this message is already waiting to be sent.', fieldErrors: {} };

describe('deliveryLogReducer', () => {
  it('DLG-1 opening starts from a clean slate and asks for the list', () => {
    const state = deliveryLogReducer({ ...initialDeliveryLogState, notice: 'old', detail, page: 4 }, DeliveryLogPageActions.opened({ orderId: null }));

    expect(state.loading).toBe(true);
    expect(state.notice).toBeNull();
    expect(state.detail).toBeNull();
    expect(state.page).toBe(1);
  });

  it('DLG-9 opening for one order filters the list to that order', () => {
    const state = deliveryLogReducer(initialDeliveryLogState, DeliveryLogPageActions.opened({ orderId: 7 }));

    expect(state.filters).toEqual({ ...NO_FILTERS, orderId: 7 });
  });

  it('DLG-1 a loaded page fills the table and the total', () => {
    const state = deliveryLogReducer(initialDeliveryLogState, DeliveryLogApiActions.loadSucceeded({ result: { items: [row], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));

    expect(state.rows).toEqual([row]);
    expect(state.total).toBe(1);
    expect(state.loading).toBe(false);
  });

  it('DLG-5 changing a filter goes back to the first page and loads again', () => {
    const state = deliveryLogReducer({ ...initialDeliveryLogState, page: 3 }, DeliveryLogPageActions.filtersChanged({ filters: { ...NO_FILTERS, deadLettersOnly: true } }));

    expect(state.page).toBe(1);
    expect(state.loading).toBe(true);
    expect(state.filters.deadLettersOnly).toBe(true);
  });

  it('DLG-8 a resend in flight makes the button wait, and success announces it', () => {
    const waiting = deliveryLogReducer({ ...initialDeliveryLogState, detail }, DeliveryLogPageActions.resendConfirmed({ id: 11, key: 'rs-abc', acknowledgeDuplicate: false }));
    const done = deliveryLogReducer(waiting, DeliveryLogApiActions.resendSucceeded({ id: 12 }));

    expect(waiting.resending).toBe(true);
    expect(done.resending).toBe(false);
    expect(done.notice).toContain('queued');
  });

  it('DLG-8 a refused resend shows the server words and stops waiting', () => {
    const waiting = deliveryLogReducer(initialDeliveryLogState, DeliveryLogPageActions.resendConfirmed({ id: 11, key: 'rs-abc', acknowledgeDuplicate: false }));
    const failed = deliveryLogReducer(waiting, DeliveryLogApiActions.resendFailed({ error: apiError }));

    expect(failed.resending).toBe(false);
    expect(failed.error?.message).toContain('already waiting');
  });

  it('DLG-1 opening a message clears the old one and loads; closing drops it', () => {
    const loading = deliveryLogReducer({ ...initialDeliveryLogState, detail }, DeliveryLogPageActions.detailOpened({ id: 12 }));
    const shown = deliveryLogReducer(loading, DeliveryLogApiActions.detailSucceeded({ detail }));
    const closed = deliveryLogReducer(shown, DeliveryLogPageActions.detailClosed());

    expect(loading.detail).toBeNull();
    expect(loading.detailLoading).toBe(true);
    expect(shown.detail).toBe(detail);
    expect(closed.detail).toBeNull();
  });
});
