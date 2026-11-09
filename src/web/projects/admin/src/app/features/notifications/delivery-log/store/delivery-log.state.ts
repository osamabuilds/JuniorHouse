import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { DeliveryDetail, DeliveryFilters, DeliveryLogRow, DeliverySummary, NO_FILTERS } from '../models';

export const DELIVERY_LOG_FEATURE_KEY = 'deliveryLog';

export interface DeliveryLogState {
  readonly rows: readonly DeliveryLogRow[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly filters: DeliveryFilters;
  readonly loading: boolean;
  readonly summary: DeliverySummary | null;
  readonly detail: DeliveryDetail | null;
  readonly detailLoading: boolean;
  /** A resend is on its way, so its button waits (DLG-8). */
  readonly resending: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialDeliveryLogState: DeliveryLogState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  filters: NO_FILTERS,
  loading: false,
  summary: null,
  detail: null,
  detailLoading: false,
  resending: false,
  error: null,
  notice: null,
};
