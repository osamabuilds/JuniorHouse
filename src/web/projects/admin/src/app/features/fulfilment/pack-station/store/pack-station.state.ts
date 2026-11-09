import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { QueueItem } from '../../pick-lists';
import { PackScanOutcome, PackView, WeightOutcome } from '../models';

export const PACK_STATION_FEATURE_KEY = 'packStation';

export interface PackStationState {
  readonly queue: readonly QueueItem[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly queueLoading: boolean;
  readonly view: PackView | null;
  readonly viewLoading: boolean;
  readonly lastScan: PackScanOutcome | null;
  readonly weight: WeightOutcome | null;
  readonly busy: boolean;
  /** Set when someone else holds the parcel, or the order cannot be packed: the screen is read-only. */
  readonly blockedReason: string | null;
  /** The parcel is sealed. */
  readonly packed: boolean;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialPackStationState: PackStationState = {
  queue: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  queueLoading: false,
  view: null,
  viewLoading: false,
  lastScan: null,
  weight: null,
  busy: false,
  blockedReason: null,
  packed: false,
  error: null,
  notice: null,
};
