import { ShipmentDetail, ShipmentRow, errorWords } from '../models';
import { BookingsApiActions, ShipmentDetailPageActions, ShipmentsPageActions } from './bookings.actions';
import { bookingsReducer } from './bookings.reducer';
import { initialBookingsState } from './bookings.state';

const row: ShipmentRow = {
  id: 3, orderId: 5, orderNumber: 'RMP-5', status: 'BOOKED', statusName: 'Booked', courierName: 'Primary courier', waybill: 'PRM-1', statusAt: '2026-11-09T10:00:00Z',
  failedAttempts: 0, needsStaff: false, cashToCollect: 4700,
};
const detail = { row, events: [], attempts: 1, lastError: null } as unknown as ShipmentDetail;

describe('bookingsReducer (spec BKG-1, MBQ-3, TRK-13)', () => {
  it('opening the list starts from a clean slate', () => {
    const state = bookingsReducer({ ...initialBookingsState, notice: 'old', page: 4, status: 'DELIVERED' }, ShipmentsPageActions.opened());

    expect(state.loading).toBe(true);
    expect(state.page).toBe(1);
    expect(state.status).toBe('');
    expect(state.notice).toBeNull();
  });

  it('choosing a status goes back to the first page', () => {
    const state = bookingsReducer({ ...initialBookingsState, page: 5 }, ShipmentsPageActions.statusChosen({ status: 'FAILED_ATTEMPT' }));

    expect(state.page).toBe(1);
    expect(state.status).toBe('FAILED_ATTEMPT');
    expect(state.loading).toBe(true);
  });

  it('a loaded page fills the list and the total', () => {
    const state = bookingsReducer(initialBookingsState, BookingsApiActions.loadSucceeded({ result: { items: [row], page: 1, pageSize: 10, totalCount: 12, totalPages: 2 } }));

    expect(state.rows).toEqual([row]);
    expect(state.total).toBe(12);
  });

  it('TRK-13 opening a shipment loads its detail', () => {
    const opened = bookingsReducer(initialBookingsState, ShipmentDetailPageActions.opened({ id: 3 }));
    const loaded = bookingsReducer(opened, BookingsApiActions.detailSucceeded({ detail }));

    expect(opened.detailLoading).toBe(true);
    expect(loaded.detail).toEqual(detail);
    expect(loaded.detailLoading).toBe(false);
  });

  it('MBQ-3 a retry makes the button wait, and says whether a courier booked it', () => {
    const waiting = bookingsReducer(initialBookingsState, ShipmentDetailPageActions.retryClicked({ id: 3 }));
    const booked = bookingsReducer(waiting, BookingsApiActions.retrySucceeded({ outcome: 'Booked', id: 3 }));
    const still = bookingsReducer(waiting, BookingsApiActions.retrySucceeded({ outcome: 'Unreachable', id: 3 }));

    expect(waiting.retrying).toBe(true);
    expect(booked.retrying).toBe(false);
    expect(booked.notice).toContain('booked');
    expect(still.notice).toContain('still waiting');
  });

  it('MBQ-3 a failed retry shows the reason and frees the button', () => {
    const state = bookingsReducer({ ...initialBookingsState, retrying: true }, BookingsApiActions.retryFailed({ error: { status: 409, message: 'No.', fieldErrors: {} } }));

    expect(state.retrying).toBe(false);
    expect(state.error?.message).toBe('No.');
  });

  it('errorWords names why a booking is waiting', () => {
    expect(errorWords('UNREACHABLE')).toContain('could be reached');
    expect(errorWords('REJECTED')).toContain('refused');
    expect(errorWords(null)).toBe('');
  });
});
