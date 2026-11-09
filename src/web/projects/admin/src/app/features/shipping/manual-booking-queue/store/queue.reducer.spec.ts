import { QueueRow, reasonWords, waitingText } from '../models';
import { QueueApiActions, QueuePageActions } from './queue.actions';
import { queueReducer } from './queue.reducer';
import { initialQueueState } from './queue.state';

const row: QueueRow = { id: 3, orderId: 5, orderNumber: 'RMP-5', reason: 'UNREACHABLE', note: null, attempts: 2, needsStaff: false, waitingMinutes: 65, nextAttemptAt: null, cashToCollect: 100 };

describe('manual booking queue (spec MBQ-1 to MBQ-8)', () => {
  it('MBQ-1 a loaded page fills the queue and the total', () => {
    const state = queueReducer(initialQueueState, QueueApiActions.loadSucceeded({ result: { items: [row], page: 1, pageSize: 10, totalCount: 1, totalPages: 1 } }));

    expect(state.rows).toEqual([row]);
    expect(state.total).toBe(1);
  });

  it('MBQ-3 try again now makes that row wait, and success frees it with a message', () => {
    const waiting = queueReducer(initialQueueState, QueuePageActions.retryClicked({ id: 3 }));
    const done = queueReducer(waiting, QueueApiActions.actionSucceeded({ message: 'A courier booked the parcel.' }));

    expect(waiting.busyId).toBe(3);
    expect(done.busyId).toBeNull();
    expect(done.notice).toContain('booked');
  });

  it('MBQ-5 a refused waybill shows the reason and frees the row', () => {
    const waiting = queueReducer(initialQueueState, QueuePageActions.waybillEntered({ id: 3, courier: 'PRIMARY', waybill: 'X-1' }));
    const failed = queueReducer(waiting, QueueApiActions.actionFailed({ error: { status: 409, message: 'That waybill is already on another parcel.', fieldErrors: {} } }));

    expect(failed.busyId).toBeNull();
    expect(failed.error?.message).toContain('already on another parcel');
  });

  it('the filter goes back to the first page', () => {
    const state = queueReducer({ ...initialQueueState, page: 3 }, QueuePageActions.filterChanged({ needsStaffOnly: true }));

    expect(state.page).toBe(1);
    expect(state.needsStaffOnly).toBe(true);
  });

  it('MBQ-1, MBQ-6, MBQ-8 the reason is told in words', () => {
    expect(reasonWords(row)).toContain('tried again');
    expect(reasonWords({ ...row, needsStaff: true })).toContain('needs you');
    expect(reasonWords({ ...row, reason: 'REJECTED', note: 'Bad address', needsStaff: true })).toContain('refused the parcel: Bad address');
  });

  it('the waiting time reads as minutes and hours', () => {
    expect(waitingText(0)).toBe('just now');
    expect(waitingText(65)).toBe('1 h 5 min');
    expect(waitingText(30)).toBe('30 min');
  });
});
