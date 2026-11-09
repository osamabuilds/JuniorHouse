import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { QueueRow } from '../models';

export const QUEUE_FEATURE_KEY = 'manualQueue';

export interface QueueState {
  readonly rows: readonly QueueRow[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly needsStaffOnly: boolean;
  readonly loading: boolean;
  /** The shipment being worked on, so its buttons wait (MBQ-3). */
  readonly busyId: number | null;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialQueueState: QueueState = {
  rows: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  needsStaffOnly: false,
  loading: false,
  busyId: null,
  error: null,
  notice: null,
};
