import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { NotifyDemandRow } from '../models';

export const NOTIFY_DEMAND_FEATURE_KEY = 'notifyDemand';

export interface NotifyDemandState {
  readonly rows: readonly NotifyDemandRow[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
}

export const initialNotifyDemandState: NotifyDemandState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
};
