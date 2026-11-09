import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { ShipmentDetail, ShipmentRow } from '../models';

export const BOOKINGS_FEATURE_KEY = 'bookings';

export interface BookingsState {
  readonly rows: readonly ShipmentRow[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly status: string;
  readonly loading: boolean;
  readonly detail: ShipmentDetail | null;
  readonly detailLoading: boolean;
  /** A retry is on its way, so its button waits (MBQ-3). */
  readonly retrying: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialBookingsState: BookingsState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  status: '',
  loading: false,
  detail: null,
  detailLoading: false,
  retrying: false,
  error: null,
  notice: null,
};
