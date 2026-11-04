import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { NO_FILTERS, OrderDetail, OrderFilters, OrderListItem } from '../models';

export const ORDER_QUEUE_FEATURE_KEY = 'ordersQueue';

export interface OrderQueueState {
  readonly orders: readonly OrderListItem[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly filters: OrderFilters;
  readonly loading: boolean;
  readonly detail: OrderDetail | null;
  readonly detailLoading: boolean;
  /** An action on the open order is on its way, so its buttons wait. */
  readonly acting: boolean;
  /** A failed load, or the server words for a refusal (the order moved meanwhile). */
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialOrderQueueState: OrderQueueState = {
  orders: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  filters: NO_FILTERS,
  loading: false,
  detail: null,
  detailLoading: false,
  acting: false,
  error: null,
  notice: null,
};
