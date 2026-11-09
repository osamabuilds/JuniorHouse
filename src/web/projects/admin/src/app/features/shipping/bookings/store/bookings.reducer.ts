import { createReducer, on } from '@ngrx/store';
import { BookingsApiActions, ShipmentDetailPageActions, ShipmentsPageActions } from './bookings.actions';
import { initialBookingsState } from './bookings.state';

export const bookingsReducer = createReducer(
  initialBookingsState,

  // The store outlives the page, so opening starts from a clean slate (CONVENTIONS section 1).
  on(ShipmentsPageActions.opened, (state) => ({ ...initialBookingsState, pageSize: state.pageSize, loading: true })),
  on(ShipmentsPageActions.statusChosen, (state, { status }) => ({ ...state, status, page: 1, loading: true, error: null })),
  on(ShipmentsPageActions.pageChanged, (state, { page }) => ({ ...state, page, loading: true, error: null })),
  on(ShipmentsPageActions.pageSizeChanged, (state, { pageSize }) => ({ ...state, pageSize, page: 1, loading: true, error: null })),
  on(ShipmentsPageActions.refreshed, (state) => ({ ...state, loading: true, error: null })),
  on(ShipmentDetailPageActions.opened, (state) => ({ ...state, detail: null, detailLoading: true, error: null, notice: null })),
  on(ShipmentDetailPageActions.retryClicked, (state) => ({ ...state, retrying: true, error: null, notice: null })),
  on(ShipmentDetailPageActions.noticeDismissed, (state) => ({ ...state, notice: null })),

  on(BookingsApiActions.loadSucceeded, (state, { result }) => ({ ...state, rows: result.items, total: result.totalCount, page: result.page, loading: false })),
  on(BookingsApiActions.loadFailed, (state, { error }) => ({ ...state, error, loading: false })),
  on(BookingsApiActions.detailSucceeded, (state, { detail }) => ({ ...state, detail, detailLoading: false })),
  on(BookingsApiActions.detailFailed, (state, { error }) => ({ ...state, error, detailLoading: false })),
  on(BookingsApiActions.retrySucceeded, (state, { outcome }) => ({
    ...state,
    retrying: false,
    notice: outcome === 'Booked' ? 'A courier booked the parcel.' : 'The booking was tried and is still waiting. See the reason below.',
  })),
  on(BookingsApiActions.retryFailed, (state, { error }) => ({ ...state, retrying: false, error })),
);
