import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { PickList, QueueItem, ScanOutcome } from '../models';

export const PICK_LISTS_FEATURE_KEY = 'pickLists';

export interface PickListsState {
  readonly queue: readonly QueueItem[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly queueLoading: boolean;
  /** The pick list being worked, or null. */
  readonly list: PickList | null;
  readonly listLoading: boolean;
  /** The answer to the last scan, shown large until the next one (APP-4). */
  readonly lastScan: ScanOutcome | null;
  /** A scan or a completion is on its way, so the scan box waits (APP-6). */
  readonly busy: boolean;
  /** A line was flagged and the pick list is being refreshed. */
  readonly raising: boolean;
  /** Set when someone else holds the order (PCK-9): the screen is read-only. */
  readonly heldBy: string | null;
  readonly error: ApiError | null;
  readonly notice: string | null;
}

export const initialPickListsState: PickListsState = {
  queue: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  queueLoading: false,
  list: null,
  listLoading: false,
  lastScan: null,
  busy: false,
  raising: false,
  heldBy: null,
  error: null,
  notice: null,
};
