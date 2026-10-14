import { ApiError } from '@core/http';
import { DEFAULT_PAGE_SIZE } from '@shared';
import { AdjustmentDto, AdjustmentReasonDto, PendingReconfirm } from '../models';

export const ADJUSTMENTS_FEATURE_KEY = 'warehouseAdjustments';

export interface AdjustmentsState {
  readonly adjustments: readonly AdjustmentDto[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly loading: boolean;
  readonly error: ApiError | null;
  readonly reasonId: number | null;
  readonly reasons: readonly AdjustmentReasonDto[];

  /** The new-adjustment form and how its submit is going. */
  readonly formOpen: boolean;
  readonly saving: boolean;
  readonly formError: ApiError | null;

  /** Set when a change above the limit was refused until the person confirms it (ADJ-4b). */
  readonly reconfirm: PendingReconfirm | null;
  readonly notice: string | null;
}

export const initialAdjustmentsState: AdjustmentsState = {
  adjustments: [],
  total: 0,
  page: 1,
  pageSize: DEFAULT_PAGE_SIZE,
  loading: false,
  error: null,
  reasonId: null,
  reasons: [],
  formOpen: false,
  saving: false,
  formError: null,
  reconfirm: null,
  notice: null,
};
