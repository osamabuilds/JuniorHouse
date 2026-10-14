import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { BinRowDto } from '../models';

export const BINS_FEATURE_KEY = 'warehouseBins';

export interface BinsState {
  readonly bins: readonly BinRowDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly zone: string;

  /** Whether the add-bin form is open, and how its submit is going. */
  readonly formOpen: boolean;
  readonly saving: boolean;
  readonly formError: ApiError | null;

  /** The bin whose active switch is being saved, so only that button is disabled. */
  readonly togglingId: number | null;
  readonly notice: string | null;
}

export const initialBinsState: BinsState = {
  bins: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  zone: '',
  formOpen: false,
  saving: false,
  formError: null,
  togglingId: null,
  notice: null,
};
