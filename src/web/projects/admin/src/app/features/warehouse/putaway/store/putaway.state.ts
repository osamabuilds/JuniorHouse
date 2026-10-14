import { ApiError } from '@core/http';
import { PutawayItemRowDto, PutawayRecordDto, PutawaySelection, ScanLookupDto } from '../models';

export const PUTAWAY_FEATURE_KEY = 'warehousePutaway';

export interface PutawayState {
  /** The queue of items still to be shelved. */
  readonly items: readonly PutawayItemRowDto[];
  readonly itemsTotal: number;
  readonly itemsPage: number;
  /** Finds one item in the queue by SKU or barcode: the way in when a variant has no tag to scan (PUT-19). */
  readonly itemSearch: string;
  readonly loading: boolean;
  readonly error: ApiError | null;

  /** The barcode scan: the answer, or a message when it is unknown, malformed or retired. */
  readonly scanning: boolean;
  readonly scanResult: ScanLookupDto | null;
  readonly scanMessage: string | null;

  /** The item being put away, and how the confirmation is going. */
  readonly selection: PutawaySelection | null;
  readonly saving: boolean;
  readonly confirmError: ApiError | null;
  readonly notice: string | null;

  /** Recent putaways, newest first. */
  readonly records: readonly PutawayRecordDto[];
  readonly recordsTotal: number;
  readonly recordsPage: number;
  readonly recordsLoading: boolean;

  /** Bumped after every success so the scan box takes focus again for the next scan (PUT-14). */
  readonly focusToken: number;
}

export const initialPutawayState: PutawayState = {
  items: [],
  itemsTotal: 0,
  itemsPage: 1,
  itemSearch: '',
  loading: false,
  error: null,
  scanning: false,
  scanResult: null,
  scanMessage: null,
  selection: null,
  saving: false,
  confirmError: null,
  notice: null,
  records: [],
  recordsTotal: 0,
  recordsPage: 1,
  recordsLoading: false,
  focusToken: 0,
};
